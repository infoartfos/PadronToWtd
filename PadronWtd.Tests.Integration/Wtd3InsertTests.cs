using System;
using FluentAssertions;
using PadronWtd.Repository.DI;
using PadronWtd.Tests.Integration.Fixtures;
using SAPbobsCOM;
using Xunit;

namespace PadronWtd.Tests.Integration
{
    public class Wtd3InsertTests : IClassFixture<SapConnectionFixture>
    {
        private readonly Company _company;
        private readonly PSaltaRepository _repository;

        public Wtd3InsertTests(SapConnectionFixture fixture)
        {
            _company = fixture.Company;
            _repository = new PSaltaRepository(_company);
        }

        [Fact]
        public void InsertWtd3_NewRecord_ShouldCreateRow()
        {
            // Arrange
            _company.StartTransaction();

            try
            {
                // Act
                var (success, error) = _repository.InsertWtd3Direct(
                    _company, TestData.AbsEntry, TestData.WtcCode,
                    TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");

                // Assert
                success.Should().BeTrue("el insert debería completarse sin error");
                error.Should().BeEmpty();

                bool exists = _repository.CheckWtd3Exists(
                    TestData.AbsEntry, TestData.WtcCode, TestData.ExistingCuit, TestData.Desde);
                exists.Should().BeTrue("el registro debería existir después del insert");
            }
            finally
            {
                if (_company.InTransaction)
                    _company.EndTransaction(BoWfTransOpt.wf_RollBack);
            }
        }

        [Fact]
        public void InsertWtd3_Duplicate_ShouldReturnSuccessAndSkip()
        {
            // Arrange
            _company.StartTransaction();

            try
            {
                // Act - primera inserción
                var (firstSuccess, firstError) = _repository.InsertWtd3Direct(
                    _company, TestData.AbsEntry, TestData.WtcCode,
                    TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");

                firstSuccess.Should().BeTrue();
                firstError.Should().BeEmpty();

                // Act - segunda inserción (duplicado)
                var (secondSuccess, secondError) = _repository.InsertWtd3Direct(
                    _company, TestData.AbsEntry, TestData.WtcCode,
                    TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");

                // Assert
                secondSuccess.Should().BeTrue("el duplicado debería reportar éxito por el skip");
                secondError.Should().BeEmpty();
            }
            finally
            {
                if (_company.InTransaction)
                    _company.EndTransaction(BoWfTransOpt.wf_RollBack);
            }
        }

        [Fact]
        public void InsertWtd3_CheckWtd3Exists_ShouldReturnFalseAfterRollback()
        {
            // Arrange
            _company.StartTransaction();

            // Act - insertar dentro de transacción
            _repository.InsertWtd3Direct(
                _company, TestData.AbsEntry, TestData.WtcCode,
                TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");

            _company.EndTransaction(BoWfTransOpt.wf_RollBack);

            // Assert
            bool exists = _repository.CheckWtd3Exists(
                TestData.AbsEntry, TestData.WtcCode, TestData.ExistingCuit, TestData.Desde);
            exists.Should().BeFalse("el rollback debería descartar el registro insertado");
        }
    }
}
