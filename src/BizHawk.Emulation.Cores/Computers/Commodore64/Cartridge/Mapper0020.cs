using System.Collections.Generic;
using System.IO;
using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

/// <summary>
/// Implements the EasyFlash cartridge format.
///
/// The most common EasyFlash implementation uses 2x AM29F040 programmable ROMs
/// and a 256-byte memory.
///
/// The address bus is 19 bits wide. Bits 18-13 are set by the "bank"
/// register (implemented as a separate bank of flip-flops on the board) and
/// bits 12-0 are set from the system bus. "RomH" and "RomL" are directly
/// tied to the respective chip-enable signals for each flash ROM, which means
/// that address range $8000-$9FFF will correspond to one flash ROM, and $A000-$BFFF
/// (or $E000-$FFFF in UltiMax configuration) will correspond to the other.
///
/// Control registers are mapped to $DE00 and $DE02. The 256-byte RAM is mapped to $DF00-$DFFF.
/// </summary>
/// <remarks>
/// Two registers can be accessed:
///
/// $DE00 - bank register (bits: 00BBBBBB)
/// B = bank ($00-$3F)
///
/// $DE02 - control register (bits: L0000MXG)
/// L = light control
/// M = Game pin control; 1=software controlled, 0=onboard jumper controlled
/// X = ExRom pin level; 1=low, 0=high
/// G = Game pin level; 1=low, 0=high
/// </remarks>
internal sealed class Mapper0020 : CartridgeDevice, ISaveRam, IDriveLight
{
	private readonly byte[] _originalMediaA; // 8000
	private readonly byte[] _originalMediaB; // A000

	private byte[] _deltaA; // 8000
	private byte[] _deltaB; // A000

	private readonly Am29F040B _chipA = new();
	private readonly Am29F040B _chipB = new();
		
	private bool _saveRamDirty;

	private bool _boardLed;
	private bool _jumper;
	private byte _stateBits;

	private byte[] _ram = new byte[256];
	private byte _bankNumber;

	public Mapper0020(IReadOnlyList<CartridgeChip> chips)
	{
		// force ultimax mode (the cart SHOULD set this
		// otherwise on load, according to the docs)
		pinGame = false;
		pinExRom = true;

		// load in all banks
		foreach (var chip in chips)
		{
			switch (chip.Address)
			{
				case 0x8000:
					chip.Data
						.ToArray()
						.CopyTo(_chipA.Data.Slice(chip.Bank * 0x2000, 0x2000));
					break;
				case 0xA000:
				case 0xE000:
					chip.Data
						.ToArray()
						.CopyTo(_chipB.Data.Slice(chip.Bank * 0x2000, 0x2000));
					break;
			}
		}

		// default to bank 0
		_bankNumber = 0;

		// back up original media
		_originalMediaA = _chipA.Data.ToArray();
		_originalMediaB = _chipB.Data.ToArray();
	}

	public override void HardReset()
	{
		_chipA.Reset();
		_chipB.Reset();
		base.HardReset();
	}

	private void FlushSaveRam()
	{
		if (_chipA.CheckDataDirty() || _deltaA == null)
			_deltaA = DeltaSerializer.GetDelta<byte>(_originalMediaA, _chipA.Data).ToArray();
			
		if (_chipB.CheckDataDirty() || _deltaB == null)
			_deltaB = DeltaSerializer.GetDelta<byte>(_originalMediaB, _chipB.Data).ToArray();

		_saveRamDirty = false;
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		if (!ser.IsReader)
			FlushSaveRam();

		ser.Sync("BankNumber", ref _bankNumber);
		ser.Sync("BoardLed", ref _boardLed);
		ser.Sync("Jumper", ref _jumper);
		ser.Sync("StateBits", ref _stateBits);
		ser.Sync("RAM", ref _ram, useNull: false);
		ser.Sync("MediaStateA", ref _deltaA, useNull: false);
		ser.Sync("MediaStateB", ref _deltaB, useNull: false);

		ser.BeginSection("FlashA");
		_chipA.SyncState(ser, withData: false);
		ser.EndSection();

		ser.BeginSection("FlashB");
		_chipB.SyncState(ser, withData: false);
		ser.EndSection();

		if (ser.IsReader)
		{
			if (_deltaA != null)
				DeltaSerializer.ApplyDelta(_originalMediaA, _chipA.Data, _deltaA);
				
			if (_deltaB != null)
				DeltaSerializer.ApplyDelta(_originalMediaB, _chipB.Data, _deltaB);
		}
	}

	private uint CalculateBankOffset(ushort addr) =>
		unchecked((uint) ((addr & 0x1FFF) | (_bankNumber << 13)));
		
	public override byte Peek8000(ushort addr) => 
		_chipA.Peek(CalculateBankOffset(addr));

	public override byte PeekA000(ushort addr) => 
		_chipB.Peek(CalculateBankOffset(addr));

	public override byte PeekDE00(ushort addr)
	{
		// normally you can't read these regs
		// but Peek is provided here for debug reasons
		// and may not stay around
		addr &= 0x02;
		return addr == 0x00 ? _bankNumber : _stateBits;
	}

	public override void Poke8000(ushort addr, byte val) =>
		_chipA.Poke(addr, val);
		
	public override void PokeA000(ushort addr, byte val) =>
		_chipB.Poke(addr, val);

	public override byte PeekDF00(ushort addr)
	{
		addr &= 0xFF;
		return _ram[addr];
	}

	public override void PokeDE00(ushort addr, byte val)
	{
		addr &= 0x02;
		if (addr == 0x00)
		{
			_bankNumber = unchecked((byte) (val & 0x3F));
		}
		else
		{
			StateSet(val);
		}
	}

	public override void PokeDF00(ushort addr, byte val)
	{
		addr &= 0xFF;
		_ram[addr] = val;
	}

	public override byte Read8000(ushort addr) => 
		_chipA.Read(CalculateBankOffset(addr));

	public override byte ReadA000(ushort addr) =>
		_chipB.Read(CalculateBankOffset(addr));

	public override byte ReadDF00(ushort addr)
	{
		addr &= 0xFF;
		return _ram[addr];
	}

	private void StateSet(byte val)
	{
		val &= 0x87;
		_stateBits = val;
		if ((val & 0x04) != 0)
		{
			pinGame = (val & 0x01) == 0;
		}
		else
		{
			pinGame = _jumper;
		}

		pinExRom = (val & 0x02) == 0;
		_boardLed = (val & 0x80) != 0;
	}

	public override void Write8000(ushort addr, byte val)
	{
		if (pinGame || !pinExRom)
			return;

		_chipA.Write(CalculateBankOffset(addr), val);
	}

	public override void WriteA000(ushort addr, byte val)
	{
		if (pinGame || !pinExRom)
			return;

		_chipB.Write(CalculateBankOffset(addr), val);
	}

	public override void WriteDE00(ushort addr, byte val)
	{
		addr &= 0x02;
		if (addr == 0x00)
		{
			_bankNumber = unchecked((byte) (val & 0x3F));
		}
		else
		{
			StateSet(val);
		}
	}

	public override void WriteDF00(ushort addr, byte val)
	{
		_ram[addr] = val;
	}

	public override void ExecutePhase()
	{
		_chipA.Clock();
		_chipB.Clock();
		_saveRamDirty |= _chipA.IsDataDirty | _chipB.IsDataDirty;
	}

	public override IEnumerable<MemoryDomain> CreateMemoryDomains()
	{
		yield return _chipA.CreateMemoryDomain("EF LoROM");
		yield return _chipB.CreateMemoryDomain("EF HiROM");

		yield return new MemoryDomainByteArray(
			name: "EF RAM",
			endian: MemoryDomain.Endian.Little,
			data: _ram,
			writable: true,
			wordSize: 1
		);
	}

	public byte[] CloneSaveRam()
	{
		FlushSaveRam();

		using var result = new MemoryStream();
		using var writer = new BinaryWriter(result);

		writer.Write(_deltaA.Length);
		writer.Write(_deltaA);
		writer.Write(_deltaB.Length);
		writer.Write(_deltaB);
		writer.Flush();

		_saveRamDirty = false;
		return result.ToArray();
	}

	/// <summary>
	/// Applies a SaveRam block to the flash memory.
	/// </summary>
	public void StoreSaveRam(byte[] data)
	{
		using var stream = new MemoryStream(data);
		using var reader = new BinaryReader(stream);
			
		var deltaASize = reader.ReadInt32();
		_deltaA = reader.ReadBytes(deltaASize);
		var deltaBSize = reader.ReadInt32();
		_deltaB = reader.ReadBytes(deltaBSize);

		DeltaSerializer.ApplyDelta(_originalMediaA, _chipA.Data, _deltaA);
		DeltaSerializer.ApplyDelta(_originalMediaB, _chipB.Data, _deltaB);
		_saveRamDirty = false;
	}

	public bool SaveRamModified => _saveRamDirty;
	public bool DriveLightEnabled => true;
	public bool DriveLightOn => _boardLed;
	public string DriveLightIconDescription => "EasyFlash LED";
}