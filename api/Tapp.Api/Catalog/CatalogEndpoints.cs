using Microsoft.EntityFrameworkCore;
using Tapp.Api.Auth;
using Tapp.Api.Data;

namespace Tapp.Api.Catalog;

public record StockAmountDto(int StockItemId, int Amount);
public record OptionDto(int? Id, string Name, List<StockAmountDto> Stock);
public record OptionGroupDto(int? Id, string Name, List<OptionDto> Options);
public record ProductDto(int? Id, string Name, string? Description, string? PicturePath, string Category,
    int PriceCents, bool Is18Plus, bool IsHidden, List<StockAmountDto> Stock, List<OptionGroupDto> OptionGroups);

public static class CatalogEndpoints
{
    private static readonly Dictionary<string, string> PictureTypes = new()
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp"
    };
    private const long MaxPictureBytes = 2 * 1024 * 1024;

    public static void MapCatalog(this WebApplication app)
    {
        var products = app.MapGroup("/api/products");

        // Hidden products are for admins only.
        products.MapGet("", async (HttpContext http, AppDbContext db) =>
        {
            var q = IncludeAll(db.Products.AsNoTracking());
            if (!http.User.IsInRole(Roles.Admin)) q = q.Where(p => !p.IsHidden);
            return (await q.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync()).Select(ToDto);
        }).RequireAuthorization();

        products.MapPost("", async (ProductDto d, AppDbContext db) =>
        {
            if (await Validate(d, db) is { } problem) return problem;
            var p = new Product();
            Apply(p, d);
            db.Products.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/products/{p.Id}", ToDto(p));
        }).RequireAuthorization(Roles.Admin);

        products.MapPut("{id:int}", async (int id, ProductDto d, AppDbContext db) =>
        {
            var p = await IncludeAll(db.Products).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (await Validate(d, db) is { } problem) return problem;
            Apply(p, d);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(p));
        }).RequireAuthorization(Roles.Admin);

        // Antiforgery off: the auth cookie is SameSite=Lax, so cross-site form posts carry no cookie.
        products.MapPost("{id:int}/picture", async (int id, IFormFile file, AppDbContext db, IConfiguration config) =>
        {
            if (!PictureTypes.TryGetValue(file.ContentType, out var ext) || file.Length is 0 or > MaxPictureBytes)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Must be a jpg, png or webp up to 2 MB"] });
            var p = await db.Products.FindAsync(id);
            if (p is null) return Results.NotFound();

            // Name comes from the id and a fixed extension, never from the upload.
            var name = $"{id}-{Guid.NewGuid():N}{ext}";
            await using (var stream = File.Create(Path.Combine(PicturesDir(config), name)))
                await file.CopyToAsync(stream);
            p.PicturePath = $"/pictures/{name}";
            await db.SaveChangesAsync();
            return Results.Ok(new { p.PicturePath });
        }).RequireAuthorization(Roles.Admin).DisableAntiforgery();
    }

    public static string PicturesDir(IConfiguration config) =>
        Path.GetFullPath(config["Pictures:Path"] ?? "pictures");

    private static IQueryable<Product> IncludeAll(IQueryable<Product> q) =>
        q.Include(p => p.Stock).Include(p => p.OptionGroups).ThenInclude(g => g.Options).ThenInclude(o => o.Stock);

    private static async Task<IResult?> Validate(ProductDto d, AppDbContext db)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(d.Name)) errors["name"] = ["Required"];
        if (string.IsNullOrWhiteSpace(d.Category)) errors["category"] = ["Required"];
        if (d.PriceCents < 0) errors["priceCents"] = ["Must not be negative"];

        var amounts = d.Stock.Concat(d.OptionGroups.SelectMany(g => g.Options).SelectMany(o => o.Stock)).ToList();
        if (amounts.Any(a => a.Amount <= 0)) errors["stock"] = ["Amounts must be greater than 0"];
        if (d.OptionGroups.Any(g => string.IsNullOrWhiteSpace(g.Name) || g.Options.Count == 0 || g.Options.Any(o => string.IsNullOrWhiteSpace(o.Name))))
            errors["optionGroups"] = ["Groups need a name and at least one named option"];

        var ids = amounts.Select(a => a.StockItemId).Distinct().ToList();
        if (await db.StockItems.CountAsync(s => ids.Contains(s.Id)) != ids.Count)
            errors["stock"] = ["Unknown stock item"];

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static StockAmount ToEntity(StockAmountDto d) => new() { StockItemId = d.StockItemId, Amount = d.Amount };

    // Groups and options are matched by id so ids stay stable when the admin edits the menu mid-event.
    private static void Apply(Product p, ProductDto d)
    {
        p.Name = d.Name.Trim();
        p.Description = d.Description;
        p.Category = d.Category.Trim();
        p.PriceCents = d.PriceCents;
        p.Is18Plus = d.Is18Plus;
        p.IsHidden = d.IsHidden;
        p.Stock = d.Stock.Select(ToEntity).ToList();

        p.OptionGroups.RemoveAll(g => !d.OptionGroups.Any(x => x.Id == g.Id));
        foreach (var gd in d.OptionGroups)
        {
            var g = gd.Id is null ? null : p.OptionGroups.FirstOrDefault(x => x.Id == gd.Id);
            if (g is null) p.OptionGroups.Add(g = new OptionGroup());
            g.Name = gd.Name.Trim();

            g.Options.RemoveAll(o => !gd.Options.Any(x => x.Id == o.Id));
            foreach (var od in gd.Options)
            {
                var o = od.Id is null ? null : g.Options.FirstOrDefault(x => x.Id == od.Id);
                if (o is null) g.Options.Add(o = new Option());
                o.Name = od.Name.Trim();
                o.Stock = od.Stock.Select(ToEntity).ToList();
            }
        }
    }

    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Description, p.PicturePath, p.Category,
        p.PriceCents, p.Is18Plus, p.IsHidden,
        p.Stock.Select(s => new StockAmountDto(s.StockItemId, s.Amount)).ToList(),
        p.OptionGroups.Select(g => new OptionGroupDto(g.Id, g.Name,
            g.Options.Select(o => new OptionDto(o.Id, o.Name,
                o.Stock.Select(s => new StockAmountDto(s.StockItemId, s.Amount)).ToList())).ToList())).ToList());
}
