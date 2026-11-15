using Pgvector;

namespace RagPlatform.Api.Data;

public static class PgvectorFunctions
{
    public static int VectorDimensions(Vector vector)
    {
        throw new NotSupportedException("This function is only supported in translated database queries.");
    }
}