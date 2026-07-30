using DeskNote.App.Models;
using DeskNote.App.Repositories;

if (args.Length != 2 || !int.TryParse(args[1], out var count) || count < 1)
{
    Console.Error.WriteLine("Usage: DeskNote.PerformanceData <data-directory> <count>");
    return 1;
}

var dataDirectory = Path.GetFullPath(args[0]);
var databaseFile = Path.Combine(dataDirectory, "todos.db");
Directory.CreateDirectory(dataDirectory);
await DatabaseInitializer.InitializeAsync(databaseFile);
var repository = new SqliteTodoRepository(databaseFile);
var baseline = DateTimeOffset.UtcNow;
for (var index = 0; index < count; index++)
{
    var createdAt = baseline.AddSeconds(-index);
    await repository.InsertAsync(new TodoItem(
        Guid.NewGuid(),
        $"Performance todo {index + 1}",
        "Performance test note",
        false,
        createdAt,
        createdAt,
        null));
}

Console.WriteLine($"Created {count} todos in {databaseFile}");
return 0;
