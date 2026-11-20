
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Database;

public static class SeedData
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        var existingNames = await context.Feedstocks
            .Select(f => f.Name)
            .ToListAsync();

        var feedstocks = new List<Feedstock>
        {
            // AGRO
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
                Name = "Napier Grass",
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
                Name = "Rice Straw",
                Category = FeedstockCategory.Agro,
                TsPercent = 85.00m,
                VsPercentOfTs = 80.00m,
                CnRatio = 65.00m,
                BmpNm3PerKgVs = 0.2500m,
                BulkDensityKgM3 = 120.00m,
                DegradationFactor = 0.550m,
                DefaultCostPerTon = 1800.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Maize Silage",
                Category = FeedstockCategory.Agro,
                TsPercent = 32.00m,
                VsPercentOfTs = 92.00m,
                CnRatio = 35.00m,
                BmpNm3PerKgVs = 0.4000m,
                BulkDensityKgM3 = 650.00m,
                DegradationFactor = 0.850m,
                DefaultCostPerTon = 2200.00m
            },

            // MANURE
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
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Buffalo Manure",
                Category = FeedstockCategory.Manure,
                TsPercent = 20.00m,
                VsPercentOfTs = 82.00m,
                CnRatio = 22.00m,
                BmpNm3PerKgVs = 0.3000m,
                BulkDensityKgM3 = 950.00m,
                DegradationFactor = 0.880m,
                DefaultCostPerTon = 250.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Pig Manure",
                Category = FeedstockCategory.Manure,
                TsPercent = 25.00m,
                VsPercentOfTs = 78.00m,
                CnRatio = 16.00m,
                BmpNm3PerKgVs = 0.3500m,
                BulkDensityKgM3 = 970.00m,
                DegradationFactor = 0.870m,
                DefaultCostPerTon = 400.00m
            },

            // INDUSTRIAL
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Food Processing Waste",
                Category = FeedstockCategory.Industrial,
                TsPercent = 20.00m,
                VsPercentOfTs = 90.00m,
                CnRatio = 18.00m,
                BmpNm3PerKgVs = 0.4500m,
                BulkDensityKgM3 = 850.00m,
                DegradationFactor = 0.920m,
                DefaultCostPerTon = 300.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Distillery Spent Wash",
                Category = FeedstockCategory.Industrial,
                TsPercent = 12.00m,
                VsPercentOfTs = 85.00m,
                CnRatio = 12.00m,
                BmpNm3PerKgVs = 0.3000m,
                BulkDensityKgM3 = 1050.00m,
                DegradationFactor = 0.900m,
                DefaultCostPerTon = 100.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Dairy Whey",
                Category = FeedstockCategory.Industrial,
                TsPercent = 6.00m,
                VsPercentOfTs = 95.00m,
                CnRatio = 20.00m,
                BmpNm3PerKgVs = 0.5000m,
                BulkDensityKgM3 = 1030.00m,
                DegradationFactor = 0.950m,
                DefaultCostPerTon = 150.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Brewery Spent Grain",
                Category = FeedstockCategory.Industrial,
                TsPercent = 25.00m,
                VsPercentOfTs = 85.00m,
                CnRatio = 20.00m,
                BmpNm3PerKgVs = 0.3500m,
                BulkDensityKgM3 = 600.00m,
                DegradationFactor = 0.820m,
                DefaultCostPerTon = 500.00m
            },

            // MSW
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Municipal Organic Waste",
                Category = FeedstockCategory.Msw,
                TsPercent = 30.00m,
                VsPercentOfTs = 85.00m,
                CnRatio = 25.00m,
                BmpNm3PerKgVs = 0.3800m,
                BulkDensityKgM3 = 550.00m,
                DegradationFactor = 0.850m,
                DefaultCostPerTon = 800.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Vegetable Market Waste",
                Category = FeedstockCategory.Msw,
                TsPercent = 15.00m,
                VsPercentOfTs = 90.00m,
                CnRatio = 20.00m,
                BmpNm3PerKgVs = 0.4200m,
                BulkDensityKgM3 = 700.00m,
                DegradationFactor = 0.920m,
                DefaultCostPerTon = 300.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Municipal Sewage Sludge",
                Category = FeedstockCategory.Msw,
                TsPercent = 5.00m,
                VsPercentOfTs = 65.00m,
                CnRatio = 8.00m,
                BmpNm3PerKgVs = 0.2200m,
                BulkDensityKgM3 = 1050.00m,
                DegradationFactor = 0.750m,
                DefaultCostPerTon = 100.00m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Canteen Food Waste",
                Category = FeedstockCategory.Msw,
                TsPercent = 22.00m,
                VsPercentOfTs = 92.00m,
                CnRatio = 17.00m,
                BmpNm3PerKgVs = 0.4800m,
                BulkDensityKgM3 = 800.00m,
                DegradationFactor = 0.930m,
                DefaultCostPerTon = 500.00m
            }
        };

        // Insert only feedstocks that do not already exist
        var newFeedstocks = feedstocks
            .Where(f => !existingNames.Contains(f.Name))
            .ToList();

        if (newFeedstocks.Count > 0)
        {
            await context.Feedstocks.AddRangeAsync(newFeedstocks);
            await context.SaveChangesAsync();
        }
    }
}
