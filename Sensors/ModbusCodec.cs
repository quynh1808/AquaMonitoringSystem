using System.Buffers.Binary;
using AquacultureMonitoring.Configuration;

namespace AquacultureMonitoring.Sensors;

public static class ModbusCodec
{
    public static byte[] BuildReadHoldingRegistersRequest(byte unitId, ushort address, ushort count, ushort transactionId)
    {
        var request = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), 6);
        request[6] = unitId;
        request[7] = 3;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10, 2), count);
        return request;
    }

    public static byte[] BuildRtuReadHoldingRegistersRequest(byte unitId, ushort address, ushort count)
    {
        var request = new byte[8];
        request[0] = unitId;
        request[1] = 3;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), count);
        var crc = ComputeCrc(request.AsSpan(0, 6));
        request[6] = (byte)(crc & 0xFF);
        request[7] = (byte)(crc >> 8);
        return request;
    }

    public static double Decode(ReadOnlySpan<byte> registerBytes, ModbusRegisterMapping mapping)
    {
        var requiredLength = GetRequiredByteCount(mapping.DataType);
        if (registerBytes.Length < requiredLength)
            throw new InvalidDataException($"{mapping.SensorId}: thiếu dữ liệu Modbus cho {mapping.DataType}.");

        Span<byte> ordered = stackalloc byte[requiredLength];
        registerBytes[..requiredLength].CopyTo(ordered);
        Reorder(ordered, mapping.ByteOrder);

        double value = mapping.DataType.ToUpperInvariant() switch
        {
            "UINT16" => BinaryPrimitives.ReadUInt16BigEndian(ordered),
            "INT16" => BinaryPrimitives.ReadInt16BigEndian(ordered),
            "UINT32" => BinaryPrimitives.ReadUInt32BigEndian(ordered),
            "INT32" => BinaryPrimitives.ReadInt32BigEndian(ordered),
            "FLOAT64" => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(ordered)),
            "FLOAT32" => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(ordered)),
            _ => throw new NotSupportedException($"DataType Modbus không được hỗ trợ: {mapping.DataType}.")
        };
        return value * mapping.Scale + mapping.Offset;
    }

    public static int GetRequiredByteCount(string dataType) => dataType.ToUpperInvariant() switch
    {
        "UINT16" or "INT16" => 2,
        "UINT32" or "INT32" or "FLOAT32" => 4,
        "FLOAT64" => 8,
        _ => throw new NotSupportedException($"DataType Modbus không được hỗ trợ: {dataType}.")
    };

    public static bool IsValidByteOrder(string dataType, string byteOrder)
    {
        var length = GetRequiredByteCount(dataType);
        var canonical = "ABCDEFGH"[..length];
        var normalized = string.IsNullOrWhiteSpace(byteOrder) ? canonical : byteOrder.ToUpperInvariant();
        return normalized.Length == length && normalized.OrderBy(x => x).SequenceEqual(canonical.OrderBy(x => x));
    }

    public static ushort ComputeCrc(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }

    private static void Reorder(Span<byte> bytes, string byteOrder)
    {
        var canonical = "ABCDEFGH"[..bytes.Length];
        var order = string.IsNullOrWhiteSpace(byteOrder) ? canonical : byteOrder.ToUpperInvariant();
        if (order.Length != bytes.Length || !order.OrderBy(x => x).SequenceEqual(canonical.OrderBy(x => x)))
            throw new InvalidDataException($"ByteOrder '{byteOrder}' không phải hoán vị hợp lệ của {canonical}.");

        var copy = bytes.ToArray();
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = copy[canonical.IndexOf(order[i])];
    }
}
