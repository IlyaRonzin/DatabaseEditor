using Microsoft.EntityFrameworkCore;

namespace DatabaseEditor.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            // Открываем подключение при создании контекста (по требованиям)
            Database.EnsureCreated();
            Database.OpenConnection();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Динамические таблицы не маппятся в EF Core, поэтому модель пустая
        }

        public async Task<List<string>> GetUserTablesAsync()
        {
            var tables = new List<string>();
            var connection = Database.GetDbConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
                ORDER BY table_name;
            ";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }
    }
}