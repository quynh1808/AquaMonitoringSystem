using System.Buffers.Binary;
using System.Net.Sockets;

namespace AquacultureMonitoring.Devices;

/// <summary>
/// Small, dependency-free Modbus TCP client for the functions required by the actuator path.
/// It opens one connection per operation so a broken PLC session cannot poison later commands.
/// </summary>
public sealed class ModbusTcpProtocol
{
    private int _transactionId;

    public async Task WriteSingleCoilAsync(
        string host, int port, byte unitId, ushort address, bool value, CancellationToken ct)
    {
        var payload = new byte[5];
        payload[0] = 0x05;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3, 2), value ? (ushort)0xFF00 : (ushort)0x0000);
        var response = await ExchangeAsync(host, port, unitId, payload, ct);
        if (!response.AsSpan().SequenceEqual(payload))
            throw new IOException("PLC trả về ACK ghi coil không khớp yêu cầu.");
    }

    public async Task<bool> ReadSingleCoilAsync(
        string host, int port, byte unitId, ushort address, CancellationToken ct)
    {
        var payload = new byte[5];
        payload[0] = 0x01;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3, 2), 1);
        var response = await ExchangeAsync(host, port, unitId, payload, ct);
        if (response.Length != 3 || response[1] != 1)
            throw new IOException("PLC trả về Read Coils không hợp lệ.");
        return (response[2] & 0x01) != 0;
    }

    public async Task<ushort> ReadHoldingRegisterAsync(
        string host, int port, byte unitId, ushort address, CancellationToken ct)
    {
        var payload = new byte[5];
        payload[0] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3, 2), 1);
        var response = await ExchangeAsync(host, port, unitId, payload, ct);
        if (response.Length != 4 || response[1] != 2)
            throw new IOException("PLC trả về Holding Register không hợp lệ.");
        return BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
    }

    public async Task WriteHoldingRegisterAsync(
        string host, int port, byte unitId, ushort address, ushort value, CancellationToken ct)
    {
        var payload = new byte[5];
        payload[0] = 0x06;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3, 2), value);
        var response = await ExchangeAsync(host, port, unitId, payload, ct);
        if (!response.AsSpan().SequenceEqual(payload))
            throw new IOException("PLC trả về ACK ghi holding register không khớp yêu cầu.");
    }

    private async Task<byte[]> ExchangeAsync(
        string host, int port, byte unitId, byte[] pdu, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        await using var stream = client.GetStream();

        var transactionId = unchecked((ushort)Interlocked.Increment(ref _transactionId));
        var request = new byte[7 + pdu.Length];
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transactionId);
        // Protocol identifier is zero for Modbus TCP.
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), checked((ushort)(pdu.Length + 1)));
        request[6] = unitId;
        pdu.CopyTo(request, 7);
        await stream.WriteAsync(request, ct);

        var header = new byte[7];
        await ReadExactAsync(stream, header, ct);
        if (BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2)) != transactionId ||
            BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2)) != 0 || header[6] != unitId)
            throw new IOException("MBAP header từ PLC không khớp request.");

        var responseLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1;
        if (responseLength is <= 0 or > 253) throw new IOException("Độ dài Modbus PDU không hợp lệ.");
        var response = new byte[responseLength];
        await ReadExactAsync(stream, response, ct);
        if ((response[0] & 0x80) != 0)
            throw new IOException($"PLC trả về Modbus exception 0x{response.ElementAtOrDefault(1):X2}.");
        if (response[0] != pdu[0]) throw new IOException("Function code Modbus phản hồi không khớp.");
        return response;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) throw new EndOfStreamException("PLC đóng kết nối trước khi trả đủ dữ liệu.");
            offset += read;
        }
    }
}
