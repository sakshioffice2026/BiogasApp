using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Database;

public static class SeedData
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        if (await context.Feedstocks.AnyAsync())
        {
            return;
        }

        var feedstocks = new List<Feedstock>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Press Mud (Sugarcane Filter Cake)",
                Category = FeedstockCategory.Agro,
                TsPercent = 25.00m,
                VsPercentOfTs = 75.00m,
                CnRatio = 24.00m,
                BmpNm3PerKgVs = 0.3400m,
                BulkDensityKgM3 = 750.00m,
                DegradationFactor = 0.880m,
                DefaultCostPerTon = 450.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Napier Grass (Energy Crop)",
                Category = FeedstockCategory.Agro,
                TsPercent = 22.00m,
                VsPercentOfTs = 88.00m,
                CnRatio = 32.00m,
                BmpNm3PerKgVs = 0.3800m,
                BulkDensityKgM3 = 400.00m,
                DegradationFactor = 0.820m,
                DefaultCostPerTon = 1200.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Cattle Dung (Dairy Slurry)",
                Category = FeedstockCategory.Manure,
                TsPercent = 18.00m,
                VsPercentOfTs = 80.00m,
                CnRatio = 20.00m,
                BmpNm3PerKgVs = 0.2800m,
                BulkDensityKgM3 = 980.00m,
                DegradationFactor = 0.900m,
                DefaultCostPerTon = 200.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Chicken Litter",
                Category = FeedstockCategory.Manure,
                TsPercent = 45.00m,
                VsPercentOfTs = 70.00m,
                CnRatio = 10.00m,
                BmpNm3PerKgVs = 0.4200m,
                BulkDensityKgM3 = 600.00m,
                DegradationFactor = 0.800m,
                DefaultCostPerTon = 600.00m
            }
        };

        await context.Feedstocks.AddRangeAsync(feedstocks);
        await context.SaveChangesAsync();
    }
}