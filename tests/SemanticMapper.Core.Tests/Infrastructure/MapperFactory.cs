using Microsoft.Extensions.Options;

namespace SemanticMapper.Core.Tests.Infrastructure;

public static class MapperFactory
{
    public static DefaultSemanticMapper Create(
        ISemanticFieldMatcher matcher,
        Action<SemanticMapperOptions>? configure = null,
        IMappingPlanCache? cache = null,
        CapturingLogger<DefaultSemanticMapper>? logger = null)
    {
        var options = new SemanticMapperOptions();
        configure?.Invoke(options);
        return new DefaultSemanticMapper(matcher, Options.Create(options), cache ?? new InMemoryMappingPlanCache(), logger);
    }
}
