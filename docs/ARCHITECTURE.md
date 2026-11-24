# Architecture

Three small diagrams of the minimal stack. See the [README](../README.md) for the reasoning behind the choices.

## 1. Components

Everything runs in one Docker container. The API serves the built SvelteKit files from the same origin, so there is no CORS and the Identity cookie just works. Dashed boxes are planned, not built yet.

```mermaid
flowchart LR
    phone["Guest / staff phone<br/>SvelteKit SPA (static files)"]
    pwa["PWA install"]:::planned

    subgraph container["One Docker container"]
        api["ASP.NET Core API<br/>Identity cookie auth<br/>REST endpoints<br/>serves the built frontend"]
        signalr["SignalR live feed"]:::planned
        subgraph vol["/data volume"]
            db[("SQLite file<br/>tapp.db")]
            pics["pictures folder"]
        end
    end

    phone -- "HTTPS, same origin" --> api
    pwa -.-> phone
    api -- "EF Core" --> db
    api -- "product pictures" --> pics
    api -.-> signalr
    signalr -.-> phone

    classDef planned stroke-dasharray: 5 5
```

## 2. Order flow

`POST /api/orders` for a guest (or staff ordering on a guest's behalf). Every check happens on the server. Stock never blocks an order: quantities just stop at 0.

```mermaid
sequenceDiagram
    actor Guest
    participant API
    participant DB as SQLite

    Guest->>API: POST /api/orders (lines, option ids)
    API->>API: Logged in, rate limit (5 per 10 s per user)
    API->>DB: Load running event
    alt no running event
        API-->>Guest: 409
    end
    API->>API: Cart size (1 to 10 items), quantities >= 1
    API->>DB: Load products with option groups and stock
    API->>API: 18+ check on the guest the order is for
    API->>API: Exactly one option per group
    API->>DB: Begin transaction
    API->>DB: Deduct stock (floor at 0)
    API->>DB: Insert order with lines (name, options, price copied)
    API->>DB: Commit
    API-->>Guest: 201 Created (order with total)
```

Failed checks return 400 (validation), 403 (guest mismatch or 18+) or 429 (rate limit). Staff must pass a `guestId`; a guest may only order for themselves.

## 3. Data model

Key fields only. `AppUser` is the ASP.NET Core Identity user with an extra `Is18Plus` flag. `StockAmount` is an owned type stored per product or option (how much of a stock item one unit uses). `OrderLine` copies the product name, options and price at order time, so later menu edits never change history.

```mermaid
erDiagram
    AppUser {
        string Id PK
        bool Is18Plus
    }
    Event {
        int Id PK
        string Name
        bool IsRunning "only one true"
    }
    Order {
        int Id PK
        int EventId FK
        string GuestId FK
        string PlacedById FK
        string Status "New, Preparing, Served, Cancelled"
    }
    OrderLine {
        int Id PK
        int ProductId
        string ProductName "snapshot"
        int UnitPriceCents "snapshot"
        int Quantity
    }
    Product {
        int Id PK
        string Name
        int PriceCents
        bool Is18Plus
        bool IsHidden
    }
    OptionGroup {
        int Id PK
        string Name
    }
    Option {
        int Id PK
        string Name
    }
    StockAmount {
        int StockItemId FK
        int Amount
    }
    StockItem {
        int Id PK
        string Name
        int Quantity
    }
    StockLog {
        int Id PK
        int StockItemId FK
        int Delta
        string Reason
    }

    Event ||--o{ Order : has
    AppUser ||--o{ Order : "ordered for (guest)"
    AppUser ||--o{ Order : "placed by"
    Order ||--|{ OrderLine : contains
    Product ||--o{ OptionGroup : has
    OptionGroup ||--|{ Option : has
    Product ||--o{ StockAmount : deducts
    Option ||--o{ StockAmount : "deducts extra"
    StockAmount }o--|| StockItem : of
    StockItem ||--o{ StockLog : "changes logged"
```
