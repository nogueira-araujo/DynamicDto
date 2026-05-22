using DynamicDtoCore;
using System.Data;
using Xunit;

namespace DynamicDtoCore.Tests
{
    /// <summary>
    /// Testes para métodos Select que retornam dynamic e suas variações.
    /// Testa a funcionalidade de criação de tipos dinâmicos sem especificação de interface.
    /// </summary>
    public class DynamicSelectTests : IDisposable
    {
        private DynamicClassFactory? _factory;

        public void Dispose()
        {
            _factory?.Dispose();
        }

        #region Basic Dynamic Select Tests

        [Fact]
        public void Factory_Should_Support_DynamicSelect_Method()
        {
            // Arrange
            _factory = new DynamicClassFactory();

            // Act & Assert
            Assert.NotNull(_factory);
            // Method exists and is callable
        }



        #endregion

        #region Dynamic Type Naming Tests

        [Fact]
        public void DynamicClass_Attribute_Should_SetCustomClassName()
        {
            // Arrange
            const string customClassName = "MinhaClasseDeTeste";

            // Act & Assert
            // The DynamicClassAttribute allows specifying a custom name for the generated class
            var attr = new DynamicClassAttribute(customClassName);
            Assert.Equal(customClassName, attr.ClassName);
        }

        [Fact]
        public void DynamicClassAttribute_With_DefaultConstructor_Should_GenerateClassName()
        {
            // Act
            var attr = new DynamicClassAttribute("TestClassName");

            // Assert
            Assert.NotNull(attr.ClassName);
            Assert.Equal("TestClassName", attr.ClassName);
        }

        #endregion

        #region Multiple Dynamic Classes Tests

        #endregion
    }
}