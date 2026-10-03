using Microsoft.EntityFrameworkCore;
using SisMaster.Identidade.Api.Data;

namespace SisMaster.Identidade.Api.Configurations
{
    public static class DbMigrationHelpers
    {
        public static async Task MigrateDatabaseAsync(this WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await dbContext.Database.MigrateAsync();
            }
        }
    }
}
