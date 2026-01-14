using FigmaDiffBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace FigmaDiffBackend.Data;

public class DiffContext : DbContext
{
    public DiffContext(DbContextOptions<DiffContext> options) : base(options) { }

    public DbSet<Baseline> Baselines => Set<Baseline>();
    public DbSet<Comparison> Comparisons => Set<Comparison>();
    public DbSet<SlackThread> SlackThreads => Set<SlackThread>();
}
