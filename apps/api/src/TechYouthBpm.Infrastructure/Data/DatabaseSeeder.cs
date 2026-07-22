using TechYouthBpm.Infrastructure.Data.Seeders;

namespace TechYouthBpm.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        bool seedMockData = true,
        CancellationToken cancellationToken = default)
    {
        await CommunityTeamDataSeeder.SeedCommunitiesAsync(db, cancellationToken);
        await IdentityDataSeeder.SeedAsync(db, cancellationToken);
        await CommunityTeamDataSeeder.SeedTeamsAsync(db, cancellationToken);

        if (seedMockData)
        {
            await ProcessInstanceDataSeeder.SeedAsync(db, cancellationToken);
            await SportifQuickDemoSeeder.SeedAsync(db, cancellationToken);
            await WorkflowActionDemoSeeder.SeedAsync(db, cancellationToken);
        }
    }
}
