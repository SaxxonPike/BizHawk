using System.Collections.Generic;
using System.IO;
using System.Text;
using BizHawk.Common.IOExtensions;
using BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Media;

public static class CRT
{
	public static CartridgeConfig ReadConfig(BinaryReader reader)
	{
		if (new string(reader.ReadChars(16)) != "C64 CARTRIDGE   ")
		{
			return null;
		}

		var headerLength = ReadCrtInt(reader);
		var version = ReadCrtShort(reader);
		var mapper = ReadCrtShort(reader);
		var exrom = reader.ReadByte() != 0;
		var game = reader.ReadByte() != 0;

		// reserved
		reader.ReadBytes(6);

		// cartridge name
		var name = ReadCrtString(reader, 0x20);

		// extra metadata
		var extraData = headerLength > 0x40
			? reader.ReadBytes(headerLength - 0x40)
			: Array.Empty<byte>();

		return new CartridgeConfig
		{
			ExRomPin = exrom, 
			GamePin = game, 
			Name = name, 
			ExtraData = extraData, 
			Version = version,
			Mapper = mapper
		};
	}

	public static IReadOnlyList<CartridgeChip> ReadChips(BinaryReader reader)
	{
		var result = new List<CartridgeChip>();

		while (reader.PeekChar() >= 0)
		{
			var chip = ReadChip(reader);
			if (chip != null)
			{
				result.Add(chip);
			}
		}

		return result;
	}
	
	public static CartridgeChip ReadChip(BinaryReader reader)
	{
		const int headerLength = 0x10;
		
		if (new string(reader.ReadChars(4)) != "CHIP")
		{
			return null;
		}

		var length = ReadCrtInt(reader);
		var type = ReadCrtShort(reader);
		var bank = ReadCrtShort(reader);
		var address = ReadCrtShort(reader);
		var dataSize = ReadCrtShort(reader);
		var data = reader.ReadBytes(dataSize);
		length -= dataSize + headerLength;
		var extraData = (length > 0) ? reader.ReadBytes(length) : Array.Empty<byte>();
		
		return new CartridgeChip
		{
			Address = address,
			Bank = bank,
			Data = data,
			ExtraData = extraData,
			Type = (CartridgeChipType) type
		};
	}
	
	private static int ReadCrtShort(BinaryReader reader) =>
		(reader.ReadByte() << 8) |
		reader.ReadByte();

	private static int ReadCrtInt(BinaryReader reader) =>
		(reader.ReadByte() << 24) |
		(reader.ReadByte() << 16) |
		(reader.ReadByte() << 8) |
		reader.ReadByte();

	private static string ReadCrtString(BinaryReader reader, int maxLength)
	{
		var bytes = reader.ReadBytes(0x20);
		var nameLength = bytes.AsSpan().IndexOf((byte) 0x00);
		if (nameLength < 0) nameLength = maxLength;
		
		return Encoding.ASCII.GetString(bytes.AsSpan(0, nameLength));
	}
}