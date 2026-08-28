using System.Text.Json.Serialization;

namespace Budgie.Modules.Finances.Application.Import.CommitMoneyManagerImport;

// Contexto source-gen para serializar ImportRunCounts sin reflexión (NativeAOT).
// El módulo no puede referenciar Budgie.Api/BudgieJsonContext (dependencia circular),
// por lo que el handler usa este contexto local; BudgieJsonContext también registra el
// tipo para el resto de serialización de la API.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ImportRunCounts))]
internal sealed partial class ImportRunCountsJsonContext : JsonSerializerContext
{
}