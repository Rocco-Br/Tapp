using Microsoft.EntityFrameworkCore;

namespace Tapp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
