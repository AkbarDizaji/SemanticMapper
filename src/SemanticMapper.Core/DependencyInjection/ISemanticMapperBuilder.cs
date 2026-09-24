using Microsoft.Extensions.DependencyInjection;

namespace SemanticMapper;

/// <summary>Configures SemanticMapper services. Provider packages add extension methods to this type.</summary>
public interface ISemanticMapperBuilder
{
    /// <summary>Gets the service collection.</summary>
    IServiceCollection Services { get; }
}
