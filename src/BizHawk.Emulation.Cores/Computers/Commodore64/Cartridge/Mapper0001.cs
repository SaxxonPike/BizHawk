using System.Collections.Generic;

using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

internal sealed class Mapper0001 : CartridgeDevice
{
	private byte[] _ram = new byte[0x2000];
	private bool _ramEnabled;

	private readonly byte[] _rom = new byte[0x8000];

	private int _romOffset;
	private bool _cartEnabled;

	public Mapper0001(IReadOnlyList<CartridgeChip> chips)
	{
		pinExRom = false;
		pinGame = false;

		foreach (var chip in chips)
		{
			if (chip.Address == 0x8000)
			{
				chip.Data.Span.CopyTo(_rom.AsSpan(0x2000 * chip.Bank));
			}
		}

		_romOffset = 0;
		_cartEnabled = true;
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("RAM", ref _ram, useNull: false);
		ser.Sync("RAMEnabled", ref _ramEnabled);
		ser.Sync("ROMOffset", ref _romOffset);
		ser.Sync("CartEnabled", ref _cartEnabled);
	}

	public override void HardReset()
	{
		base.HardReset();
		pinExRom = false;
		pinGame = false;
		_ram.AsSpan().Clear();

		_romOffset = 0;
		_cartEnabled = true;
	}

	public override byte Peek8000(ushort addr)
	{
		return GetLoRom(addr);
	}

	public override byte PeekA000(ushort addr)
	{
		return Peek8000(addr);
	}

	public override byte PeekDF00(ushort addr)
	{
		return GetIo2(addr);
	}

	public override void Poke8000(ushort addr, byte val)
	{
		SetLoRom(addr, val);
	}

	public override void PokeA000(ushort addr, byte val)
	{
		Poke8000(addr, val);
	}

	public override void PokeDE00(ushort addr, byte val)
	{
		SetState(val);
	}

	public override void PokeDF00(ushort addr, byte val)
	{
		SetIo2(addr, val);
	}

	public override byte Read8000(ushort addr)
	{
		return GetLoRom(addr);
	}

	public override byte ReadA000(ushort addr)
	{
		return GetHiRom(addr);
	}

	public override byte ReadDF00(ushort addr)
	{
		return GetIo2(addr);
	}

	public override void Write8000(ushort addr, byte val)
	{
		SetLoRom(addr, val);
	}

	public override void WriteA000(ushort addr, byte val)
	{
		SetLoRom(addr, val);
	}

	public override void WriteDE00(ushort addr, byte val)
	{
		SetState(val);
	}

	public override void WriteDF00(ushort addr, byte val)
	{
		SetIo2(addr, val);
	}

	private void SetState(byte val)
	{
		pinGame = (val & 0x01) == 0;
		pinExRom = (val & 0x02) != 0;
		_cartEnabled = (val & 0x04) == 0;
		_romOffset = (val & 0x18) << 10;
		_ramEnabled = (val & 0x20) == 0;
	}

	private byte GetLoRom(ushort addr)
	{
		return _ramEnabled
			? _ram[addr & 0x1FFF]
			: _rom[(addr & 0x1FFF) | _romOffset];
	}

	private byte GetHiRom(ushort addr)
	{
		return _rom[(addr & 0x1FFF) | _romOffset];
	}

	private void SetLoRom(ushort addr, byte val)
	{
		_ram[addr & 0x1FFF] = val;
	}

	private byte GetIo2(ushort addr)
	{
		if (!_cartEnabled)
		{
			return ReadOpenBus();
		}

		return _ramEnabled
			? _ram[(addr & 0xFF) | 0x1F00]
			: _rom[(addr & 0xFF) | _romOffset | 0x1F00];
	}

	private void SetIo2(ushort addr, byte val)
	{
		_ram[addr & 0x1FFF] = unchecked((byte) (val & 0xFF));
	}
}