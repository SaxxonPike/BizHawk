using System.Collections.Generic;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

public static class MapperFactory
{
	public static CartridgeDevice Create(CartridgeConfig config, IReadOnlyList<CartridgeChip> chips) =>
		config.Mapper switch
		{
			0x0000 => new Mapper0000(chips, config.GamePin, config.ExRomPin),
			0x0001 => new Mapper0001(chips),
			0x0005 => new Mapper0005(chips),
			0x0007 => new Mapper0007(chips, config.GamePin, config.ExRomPin),
			0x0008 => new Mapper0008(chips),
			0x000A => new Mapper000A(chips),
			0x000B => new Mapper000B(chips),
			0x000F => new Mapper000F(chips),
			0x0011 => new Mapper0011(chips),
			0x0012 => new Mapper0012(chips),
			0x0013 => new Mapper0013(chips),
			0x0020 => new Mapper0020(chips),
			0x002B => new Mapper002B(chips),
			_ => throw new InvalidOperationException($"This cartridge file uses an unrecognized mapper: {config.Mapper:X4}")
		};
}