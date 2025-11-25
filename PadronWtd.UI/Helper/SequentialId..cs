using System;

public static class SequentialId
{
    public static string Generate()
    {
        // 1. Obtener el tiempo actual en Ticks (1 Tick = 100 nanosegundos)
        long ticks = DateTime.UtcNow.Ticks;

        // 2. Convertir a Hexadecimal (x16 asegura que tenga ceros a la izquierda si hace falta)
        // Esto ocupa 16 caracteres y garantiza el orden cronológico.
        string timePart = ticks.ToString("x16");

        // 3. Generar un GUID random para la unicidad y tomar los primeros 16 chars
        string randomPart = Guid.NewGuid().ToString("N").Substring(0, 16);

        // 4. Unir (Total 32 caracteres)
        // Ejemplo: 08dbeec4e3d38c00a4168218146f4090
        //          [--- TIEMPO ---][--- RANDOM ---]
        return timePart + randomPart;
    }
}