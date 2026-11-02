# Tapp: Features

Legend: **MVP** = first release, **Later** = planned, not built yet.

## 1. Overview

Tapp is an ordering app for home parties and small restaurants. Guests order food and drinks from their phone, and staff see orders arrive live.

- Web app and installable PWA (same codebase). UI is clean and simple.
- Scale: home use, max ~50 guests per event (usually ~25).
- Language: Dutch first, English later.
- No payments now. Orders store the price at order time so a tab/payment system can be added later without rewriting history.
- Single venue now. Venues may come later.

## 2. Roles and permissions

| Role | Can do | Status |
|---|---|---|
| Admin | Everything: products, stock, events, users, 18+ approvals, stats | MVP |
| Employee | See and fulfil orders, place orders for guests. No user, stock-setting or settings changes | MVP |
| Guest | Browse the menu, order for themselves, see their own orders | MVP |

- Guests are created by an admin with a username and password. **MVP**
- Each guest is flagged 18+ or not. The admin can set it at creation. **MVP**
- A non-18+ guest can request 18+ access. The admin approves or denies. **MVP**
- QR code or magic-link login for guests. **Later**

## 3. Catalog

- **Products**: name, description, picture, category (drinks, snacks, ...), price. **MVP**
- **Option groups**: for example Cola: Zero or Normal. Each option can change which stock is deducted. Managed from the product screen. **MVP**
- **Ingredients**: a product links to ingredients, each with a default deduction amount. An ingredient can also be a product of its own. **MVP**
- **18+ flag** on products. Enforced server-side when an order is placed, not only in the UI. **MVP**
- **Hide/unlist** a product without deleting it. **MVP**
- Sell price stored on each order line at order time. **MVP**

## 4. Inventory and stock

- Stock items have a name, unit (bottle, ml, piece) and quantity. **MVP**
- Rough tracking: ordering a product deducts the default amount for each linked ingredient. A plain item (for example a cola bottle) deducts itself. **MVP**
- Manual stock correction (restock, waste, breakage) with a log entry. **MVP**
- Low-stock indicator. **MVP**
- Out-of-stock products are shown as unavailable or hidden. **Later**

## 5. Events

- Admin starts and stops an event (a party or service session). **MVP**
- Orders belong to the running event, per guest. **MVP**
- Starting a new event: **carry over** leftover stock from the previous event, or **start empty** with a fresh inventory. **MVP**
- Event totals: everything ordered, per guest and per product, plus stock used. **MVP**

## 6. Ordering flow

- Guests browse the menu, pick options, add to the cart and place the order. **MVP**
- Staff (admin or employee) can place an order on a guest's behalf. **MVP**
- Guests see their own order history and status for the event. **MVP**
- Order status: new, preparing, served, cancelled. **MVP**
- Kitchen/bar split: each product has a station. An event setting shows split queues or one combined queue. **MVP**
- Live updates, so new orders and status changes appear without refreshing. **MVP**
- Spam prevention: rate limit on placing orders and a maximum cart size, so 20 beers cannot be ordered in one second. No per-guest quantity limits. **MVP**

## 7. Admin panel and stats

- Live order feed: time, guest, items, status. **MVP**
- Manage users, products, ingredients, stock and events. **MVP**
- Approve or deny 18+ requests. **MVP**
- Stats per event: top products, per-guest totals, stock usage. **MVP**

## 8. Later / backlog

| Idea | Notes |
|---|---|
| Order notes | For example "no ice" |
| Table or location number | Per order |
| Push notifications | "Your order is ready" for guests, new-order alerts for staff |
| QR / magic-link login | Guests without passwords |
| CSV export | Event stats |
| Offline-tolerant ordering | Queue orders while the connection drops |
| Audit log | Admin actions |
| Payments / tabs | Running bill per guest, built on stored order prices |
| Venues | Multiple venues, each with own data |
| English | Dutch first; English translation later |
| Order cancel/undo by guest | Short time window |
