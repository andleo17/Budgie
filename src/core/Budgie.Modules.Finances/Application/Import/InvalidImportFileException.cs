namespace Budgie.Modules.Finances.Application.Import;

// Excepción neutra para archivos de import no válidos (magia/estructura SQLite, tabla
// requerida ausente, tamaño excedido...). Sustituye a InvalidOperationException/SqliteException
// en la frontera de Infrastructure → Application para no fugar el tipo de infraestructura.
// Hereda de InvalidOperationException para mantener compatibilidad con catch existentes.
public sealed class InvalidImportFileException : InvalidOperationException
{
    public InvalidImportFileException(string message) : base(message)
    {
    }
}