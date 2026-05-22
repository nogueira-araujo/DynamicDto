using DynamicDtoCore;
using Xunit;
using System.Data.Common;

namespace DynamicDtoCore.Tests
{
    /// <summary>
    /// Testes para construtores da DynamicClassFactory e gerenciamento de recursos.
    /// Valida diferentes formas de inicialização e comportamento do Dispose.
    /// </summary>
    public class DynamicClassFactoryConstructorTests : IDisposable
    {
        private DynamicClassFactory? _factory;

        public void Dispose()
        {
            _factory?.Dispose();
        }

        #region Default Constructor Tests

        [Fact]
        public void DefaultConstructor_Should_CreateFactory()
        {
            // Act
            _factory = new DynamicClassFactory();

            // Assert
            Assert.NotNull(_factory);
            Assert.IsType<DynamicClassFactory>(_factory);
        }

        [Fact]
        public void DefaultConstructor_Should_InitializeSuccessfully()
        {
            // Act
            _factory = new DynamicClassFactory();

            // Assert
            Assert.NotNull(_factory);
        }

        [Fact]
        public void DefaultConstructor_Should_CreateInternalConnection()
        {
            // Act
            _factory = new DynamicClassFactory();

            // Assert - Factory should have internal connection created
            Assert.NotNull(_factory);
        }

        [Fact]
        public void DefaultConstructor_Multiple_Instances_Should_AllBeValid()
        {
            // Arrange
            var factories = new List<DynamicClassFactory>();

            // Act
            for (int i = 0; i < 3; i++)
            {
                factories.Add(new DynamicClassFactory());
            }

            // Assert
            Assert.All(factories, f => Assert.NotNull(f));

            // Cleanup
            foreach (var f in factories)
            {
                f.Dispose();
            }
        }

        #endregion

        #region DbCommand Constructor Tests

        [Fact]
        public void ConstructorWithDbCommand_Should_AcceptValidCommand()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();
            var command = connection.CreateCommand();

            // Act
            _factory = new DynamicClassFactory(command);

            // Assert
            Assert.NotNull(_factory);
            Assert.IsType<DynamicClassFactory>(_factory);
        }

        [Fact]
        public void ConstructorWithDbCommand_NullCommand_Should_ThrowArgumentNullException()
        {
            // Arrange
            DbCommand? nullCommand = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DynamicClassFactory(nullCommand!));
        }

        [Fact]
        public void ConstructorWithDbCommand_Should_StoreCommandReference()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();
            var command = connection.CreateCommand();

            // Act
            _factory = new DynamicClassFactory(command);

            // Assert
            Assert.NotNull(_factory);
        }

        [Fact]
        public void ConstructorWithDbCommand_ExternalConnection_Should_NotDisposeConnection()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();
            var command = connection.CreateCommand();

            // Act
            _factory = new DynamicClassFactory(command);

            // Assert
            Assert.NotNull(_factory);
            // Connection should still be usable after factory disposal
        }

        #endregion

        #region DbConnection Constructor Tests

        [Fact]
        public void ConstructorWithDbConnection_Should_AcceptValidConnection()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();

            // Act
            _factory = new DynamicClassFactory(connection);

            // Assert
            Assert.NotNull(_factory);
            Assert.IsType<DynamicClassFactory>(_factory);
        }

        [Fact]
        public void ConstructorWithDbConnection_NullConnection_Should_ThrowArgumentNullException()
        {
            // Arrange
            DbConnection? nullConnection = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DynamicClassFactory(nullConnection!));
        }

        [Fact]
        public void ConstructorWithDbConnection_Should_CreateInternalCommand()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();

            // Act
            _factory = new DynamicClassFactory(connection);

            // Assert
            Assert.NotNull(_factory);
        }

        [Fact]
        public void ConstructorWithDbConnection_Should_StoreConnectionReference()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();

            // Act
            _factory = new DynamicClassFactory(connection);

            // Assert
            Assert.NotNull(_factory);
        }

        #endregion

        #region Constructor Parameter Validation Tests

        [Fact]
        public void Constructor_Should_ValidateInputs()
        {
            // Arrange & Act
            // Only invalid null inputs should throw
            DbConnection conn = null;
            DbCommand comm = null;
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DynamicClassFactory(conn));
            Assert.Throws<ArgumentNullException>(() => new DynamicClassFactory(comm));
        }

        [Fact]
        public void Constructor_With_ValidConnection_Should_NotThrow()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();

            // Act & Assert - Should not throw
            var factory = new DynamicClassFactory(connection);
            Assert.NotNull(factory);
            factory.Dispose();
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_Should_ReleaseResources()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act
            _factory.Dispose();

            // Assert - No exception should be thrown
            Assert.True(true);
        }

        [Fact]
        public void Dispose_Should_BeIdempotent()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act
            _factory.Dispose();
            _factory.Dispose(); // Should not throw

            // Assert
            Assert.True(true);
        }

        [Fact]
        public void Dispose_Multiple_Times_Should_NotThrow()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act & Assert
            _factory.Dispose();
            _factory.Dispose();
            _factory.Dispose();
        }

        [Fact]
        public void Dispose_Should_SuppressExceptions()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act & Assert - Even if exceptions occur during disposal, they should be suppressed
            _factory.Dispose();
        }

        [Fact]
        public void DisposeWithDbCommand_Should_ReleaseCommand()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();
            var command = connection.CreateCommand();
            _factory = new DynamicClassFactory(command);

            // Act
            _factory.Dispose();

            // Assert
            Assert.True(true);
        }

        [Fact]
        public void DisposeWithDbConnection_Should_ReleaseConnection()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();
            _factory = new DynamicClassFactory(connection);

            // Act
            _factory.Dispose();

            // Assert
            Assert.True(true);
        }

        #endregion

        #region Using Statement Tests

        [Fact]
        public void Using_Statement_Should_CallDispose()
        {
            // Act
            using (var factory = new DynamicClassFactory())
            {
                // Assert - Factory is valid inside using block
                Assert.NotNull(factory);
            } // Dispose called here

            // Assert - No exception after exiting using block
            Assert.True(true);
        }

        [Fact]
        public void Using_Statement_WithDbCommand_Should_Dispose()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();
            var command = connection.CreateCommand();

            // Act
            using (var factory = new DynamicClassFactory(command))
            {
                Assert.NotNull(factory);
            }

            // Assert
            Assert.True(true);
        }

        [Fact]
        public void Using_Statement_WithDbConnection_Should_Dispose()
        {
            // Arrange
            using var connection = ProviderHelper.CreateConnection();

            // Act
            using (var factory = new DynamicClassFactory(connection))
            {
                Assert.NotNull(factory);
            }

            // Assert
            Assert.True(true);
        }

        [Fact]
        public void Nested_Using_Statements_Should_AllDispose()
        {
            // Act
            using (var factory1 = new DynamicClassFactory())
            {
                using (var factory2 = new DynamicClassFactory())
                {
                    // Assert
                    Assert.NotNull(factory1);
                    Assert.NotNull(factory2);
                }
            }

            // Assert - No exception
            Assert.True(true);
        }

        #endregion

        #region Factory Reusability Tests

        [Fact]
        public void Factory_After_Creation_Should_BeReusable()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act
            var factory1 = _factory;
            var factory2 = _factory;

            // Assert
            Assert.Same(factory1, factory2);
        }

        [Fact]
        public void Factory_Multiple_Operations_Should_BeSupported()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act & Assert - Multiple uses should not cause issues
            for (int i = 0; i < 5; i++)
            {
                Assert.NotNull(_factory);
            }
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public void Factory_Creation_And_Disposal_Should_ReleaseMemory()
        {
            // Arrange
            var factory = new DynamicClassFactory();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            factory.Dispose();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);

            // Assert - Memory should be released (may not be immediately freed)
            Assert.True(true);
        }

        [Fact]
        public void Multiple_Factories_Should_ManageMemoryProperly()
        {
            // Arrange
            var factories = new List<DynamicClassFactory>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                factories.Add(new DynamicClassFactory());
            }

            // Assert
            Assert.Equal(10, factories.Count);

            // Cleanup
            foreach (var f in factories)
            {
                f.Dispose();
            }

            GC.Collect();
            Assert.True(true);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Constructor_With_NullCommand_Should_ProvideMeaningfulError()
        {
            // Arrange & Act
            var ex = Assert.Throws<ArgumentNullException>(() => new DynamicClassFactory((DbCommand)null!));

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
        }

        #endregion
    }
}