using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PadronWtd.Domain;
using PadronWtd.Repository.DI;
using PadronWtd.UI.Logging;
using PadronWtd.UI.Services;
using SAPbobsCOM;
using Xunit;

namespace PadronWtd.Tests.UI.Services
{
    public class ProcessInfoServiceTests
    {
        private readonly Mock<IPSaltaWtd3Repository> _repoMock;
        private readonly Mock<ITransactionManager> _txMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IProviderChecker> _providerMock;
        private readonly ProcessInfoService _service;

        public ProcessInfoServiceTests()
        {
            _repoMock = new Mock<IPSaltaWtd3Repository>();
            _txMock = new Mock<ITransactionManager>();
            _loggerMock = new Mock<ILogger>();
            _providerMock = new Mock<IProviderChecker>();
            _providerMock.Setup(p => p.CuitExists(It.IsAny<string>())).Returns(true);
            _service = new ProcessInfoService(_repoMock.Object, _txMock.Object, _providerMock.Object, _loggerMock.Object);
        }

        private static PSaltaRecord CreateRecord(string cuit = "20000156982", string insc = "ACT1", string riesgo = "ALTA")
        {
            return new PSaltaRecord
            {
                Code = 1,
                U_Cuit = cuit,
                U_Inscripcion = insc,
                U_Riesgo = riesgo,
                U_Estado = "10"
            };
        }

        [Fact]
        public async Task Cuando_TodosLosCodigosSeInsertan_DeberiaTerminarEnEstado20()
        {
            // Arrange
            var record = CreateRecord();
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 12, 31);
            string timestamp = "2026-04-06 12:00:00";

            _txMock.Setup(x => x.InTransaction).Returns(false);
            _repoMock.Setup(r => r.CheckWtd3Exists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                     .Returns(false);
            _repoMock.Setup(r => r.InsertWtd3Direct(It.IsAny<Company>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>()))
                     .Returns((true, string.Empty));
            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PSaltaRecord>()))
                     .ReturnsAsync(string.Empty);

            // Acceder a la cache de impuestos via reflection para poblarla
            SetImpuestosCache(record.U_Inscripcion, record.U_Riesgo, "COD01", "1");

            // Act
            var result = await _service.ProcessSingleRecordAsync(record, desde, hasta, timestamp);

            // Assert
            result.Should().BeTrue();
            record.U_Estado.Should().Be("20");
            record.U_Notas.Should().Contain("Procesado OK");

            _txMock.Verify(x => x.StartTransaction(), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_Commit), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_RollBack), Times.Never);
        }

        [Fact]
        public async Task Cuando_AlgunosCodigosYaExisten_DeberiaInsertarLosDemasYTerminarEn40()
        {
            // Arrange
            var record = CreateRecord();
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 12, 31);
            string timestamp = "2026-04-06 12:00:00";

            _txMock.Setup(x => x.InTransaction).Returns(false);
            _repoMock.Setup(r => r.CheckWtd3Exists(It.IsAny<int>(), "COD01", record.U_Cuit, desde))
                     .Returns(true);  // ya existe
            _repoMock.Setup(r => r.CheckWtd3Exists(It.IsAny<int>(), "COD02", record.U_Cuit, desde))
                     .Returns(false); // no existe
            _repoMock.Setup(r => r.InsertWtd3Direct(It.IsAny<Company>(), It.IsAny<int>(), "COD02",
                    record.U_Cuit, desde, hasta, "80", "A"))
                     .Returns((true, string.Empty));
            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PSaltaRecord>()))
                     .ReturnsAsync(string.Empty);

            SetImpuestosCache(record.U_Inscripcion, record.U_Riesgo,
                ("COD01", "1"), ("COD02", "2"));

            // Act
            var result = await _service.ProcessSingleRecordAsync(record, desde, hasta, timestamp);

            // Assert
            result.Should().BeFalse();
            record.U_Estado.Should().Be("40");
            record.U_Notas.Should().Contain("COD01");

            _txMock.Verify(x => x.StartTransaction(), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_Commit), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_RollBack), Times.Never);
        }

        [Fact]
        public async Task Cuando_TodosLosCodigosYaExisten_DeberiaTerminarEn40SinInserciones()
        {
            // Arrange
            var record = CreateRecord();
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 12, 31);
            string timestamp = "2026-04-06 12:00:00";

            _txMock.Setup(x => x.InTransaction).Returns(false);
            _repoMock.Setup(r => r.CheckWtd3Exists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                     .Returns(true);  // todos existen
            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PSaltaRecord>()))
                     .ReturnsAsync(string.Empty);

            SetImpuestosCache(record.U_Inscripcion, record.U_Riesgo, "COD01", "1");

            // Act
            var result = await _service.ProcessSingleRecordAsync(record, desde, hasta, timestamp);

            // Assert
            result.Should().BeFalse();
            record.U_Estado.Should().Be("40");

            _txMock.Verify(x => x.StartTransaction(), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_Commit), Times.Once);
            _repoMock.Verify(r => r.InsertWtd3Direct(It.IsAny<Company>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Cuando_FallaInsert_DeberiaHacerRollbackYTerminarEn40()
        {
            // Arrange
            var record = CreateRecord();
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 12, 31);
            string timestamp = "2026-04-06 12:00:00";

            _txMock.SetupSequence(x => x.InTransaction)
                   .Returns(false)  // antes de empezar
                   .Returns(true);  // después de StartTransaction
            _repoMock.Setup(r => r.CheckWtd3Exists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                     .Returns(false);
            _repoMock.Setup(r => r.InsertWtd3Direct(It.IsAny<Company>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>()))
                     .Returns((false, "DB error"));
            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PSaltaRecord>()))
                     .ReturnsAsync(string.Empty);

            SetImpuestosCache(record.U_Inscripcion, record.U_Riesgo, "COD01", "1");

            // Act
            var result = await _service.ProcessSingleRecordAsync(record, desde, hasta, timestamp);

            // Assert
            result.Should().BeFalse();
            record.U_Estado.Should().Be("40");

            _txMock.Verify(x => x.StartTransaction(), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_RollBack), Times.Once);
            _txMock.Verify(x => x.EndTransaction(BoWfTransOpt.wf_Commit), Times.Never);
        }

        [Fact]
        public async Task Cuando_NoHayConfiguracionDeImpuesto_DeberiaTerminarEn40()
        {
            // Arrange: no poblar la cache de impuestos
            var record = CreateRecord();
            var desde = new DateTime(2026, 1, 1);
            var hasta = new DateTime(2026, 12, 31);
            string timestamp = "2026-04-06 12:00:00";

            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PSaltaRecord>()))
                     .ReturnsAsync(string.Empty);

            // Act
            var result = await _service.ProcessSingleRecordAsync(record, desde, hasta, timestamp);

            // Assert
            result.Should().BeFalse();
            record.U_Estado.Should().Be("40");
            record.U_Notas.Should().Be("Configuración Impuesto No Encontrada");

            _txMock.Verify(x => x.StartTransaction(), Times.Never);
            _txMock.Verify(x => x.EndTransaction(It.IsAny<BoWfTransOpt>()), Times.Never);
        }

        /// <summary>
        /// Puebla el cache interno de impuestos via reflection para evitar
        /// la dependencia con SaltaConfigRepository.
        /// </summary>
        private void SetImpuestosCache(string inscripcion, string riesgo, params (string codigoSap, string uCodigo)[] items)
        {
            var cacheField = typeof(ProcessInfoService)
                .GetField("_impuestosCache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var cache = new Dictionary<string, List<ImpuestoCacheItem>>();
            var list = new List<ImpuestoCacheItem>();
            foreach (var (codigoSap, uCodigo) in items)
            {
                list.Add(new ImpuestoCacheItem { CodigoSap = codigoSap, U_Codigo = uCodigo });
            }
            string key = $"{inscripcion.ToUpper()}_{riesgo.ToUpper()}";
            cache[key] = list;
            cacheField.SetValue(_service, cache);
        }
    }
}
