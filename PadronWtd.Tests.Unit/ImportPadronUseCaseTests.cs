using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PadronWtd.Application.Services;
using PadronWtd.Infrastructure.Repositories;
using PadronWtd.Infrastructure.Utils;
using Xunit;

namespace PadronWtd.Tests.Unit;

public class ImportPadronUseCaseTests
{
    [Fact]
    public async Task Should_Import_Tsv_File_And_Save_Entries()
    {
        // Arrange
        var repo = new InMemoryPadronRepository();
        var importer = new CsvImporter(repo);
        var useCase = new ImportPadronUseCase(importer);

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "20000156982\tProveedor A\tACT1\tALTA");

        // Act
        await useCase.ExecuteAsync(tempFile, runId: 1, user: "test");

        // Assert
        var data = await repo.GetByRunAsync(1);
        data.Should().ContainSingle(e => e.CUIT == "20000156982" && e.LineNumber == 1);

        // Limpieza
        File.Delete(tempFile);
    }
}
