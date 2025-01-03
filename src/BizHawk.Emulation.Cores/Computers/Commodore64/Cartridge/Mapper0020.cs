using System.Collections.Generic;
using System.IO;
using System.Linq;
using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge
{
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
	internal sealed class Mapper0020 : CartridgeDevice, ISaveRam
	{
		private int _bankOffset = 63 << 13;

		private int[] _banksA = new int[64 << 13]; // 8000
		private int[] _banksB = new int[64 << 13]; // A000

		private readonly int[] _originalMediaA; // 8000
		private readonly int[] _originalMediaB; // A000

		private byte[] _deltaA; // 8000
		private byte[] _deltaB; // A000
		
		private bool _bankDeltaDirty;
		private bool _saveRamDirty;

		private bool _boardLed;

		private bool _jumper;

		private int _stateBits;

		private int[] _ram = new int[256];

		private bool _commandLatch55;
		private bool _commandLatchAa;

		private int _internalRomState;
		private bool _eraseMode;
		private int _bankNumber;

		public Mapper0020(IList<int> newAddresses, IList<int> newBanks, IList<int[]> newData)
		{
			DriveLightEnabled = true;
			var count = newAddresses.Count;

			// force ultimax mode (the cart SHOULD set this
			// otherwise on load, according to the docs)
			pinGame = false;
			pinExRom = true;

			// for safety, initialize all banks to dummy
			for (var i = 0; i < 64 * 0x2000; i++)
			{
				_banksA[i] = 0xFF;
				_banksB[i] = 0xFF;
			}

			// load in all banks
			for (var i = 0; i < count; i++)
			{
				switch (newAddresses[i])
				{
					case 0x8000:
						Array.Copy(newData[i], 0, _banksA, newBanks[i] * 0x2000, 0x2000);
						break;
					case 0xA000:
					case 0xE000:
						Array.Copy(newData[i], 0, _banksB, newBanks[i] * 0x2000, 0x2000);
						break;
				}
			}

			// default to bank 0
			BankSet(0);

			// internal operation settings
			_commandLatch55 = false;
			_commandLatchAa = false;
			_internalRomState = 0;

			// back up original media
			_originalMediaA = _banksA.Select(d => d).ToArray();
			_originalMediaB = _banksB.Select(d => d).ToArray();
		}

		private void FlushSaveRam()
		{
			if (!_bankDeltaDirty)
				return;
			
			_deltaA = DeltaSerializer.GetDelta<int>(_originalMediaA, _banksA).ToArray();
			_deltaB = DeltaSerializer.GetDelta<int>(_originalMediaB, _banksB).ToArray();
			_bankDeltaDirty = false;
		}

		protected override void SyncStateInternal(Serializer ser)
		{
			if (!ser.IsReader && _bankDeltaDirty)
				FlushSaveRam();

			ser.Sync("BankOffset", ref _bankOffset);
			ser.Sync("BoardLed", ref _boardLed);
			ser.Sync("Jumper", ref _jumper);
			ser.Sync("StateBits", ref _stateBits);
			ser.Sync("RAM", ref _ram, useNull: false);
			ser.Sync("CommandLatch55", ref _commandLatchAa);
			ser.Sync("CommandLatchAA", ref _commandLatchAa);
			ser.Sync("InternalROMState", ref _internalRomState);
			ser.Sync("MediaStateA", ref _deltaA, useNull: false);
			ser.Sync("MediaStateB", ref _deltaB, useNull: false);

			if (ser.IsReader)
			{
				DeltaSerializer.ApplyDelta<int>(_originalMediaA, _banksA, _deltaA);
				DeltaSerializer.ApplyDelta<int>(_originalMediaB, _banksB, _deltaB);
				_bankDeltaDirty = false;
			}
			
			DriveLightOn = _boardLed;
		}

		private void BankSet(int index)
		{
			_bankOffset = (index & 0x3F) << 13;
		}

		public override int Peek8000(int addr)
		{
			addr &= 0x1FFF;
			return _banksA[addr | _bankOffset];
		}

		public override int PeekA000(int addr)
		{
			addr &= 0x1FFF;
			return _banksB[addr | _bankOffset];
		}

		public override int PeekDE00(int addr)
		{
			// normally you can't read these regs
			// but Peek is provided here for debug reasons
			// and may not stay around
			addr &= 0x02;
			return addr == 0x00 ? _bankOffset >> 13 : _stateBits;
		}

		public override int PeekDF00(int addr)
		{
			addr &= 0xFF;
			return _ram[addr];
		}

		public override void PokeDE00(int addr, int val)
		{
			addr &= 0x02;
			if (addr == 0x00)
			{
				BankSet(val);
			}
			else
			{
				StateSet(val);
			}
		}

		public override void PokeDF00(int addr, int val)
		{
			addr &= 0xFF;
			_ram[addr] = val & 0xFF;
		}

		public override int Read8000(int addr)
		{
			return ReadInternal(addr & 0x1FFF, _banksA);
		}

		public override int ReadA000(int addr)
		{
			return ReadInternal(addr & 0x1FFF, _banksB);
		}

		public override int ReadDF00(int addr)
		{
			addr &= 0xFF;
			return _ram[addr];
		}

		private int ReadInternal(int addr, int[] bank)
		{
			switch (_internalRomState)
			{
				case 0x80:
					break;
				case 0x90:
					switch (addr & 0x1FFF)
					{
						case 0x0000:
							return 0x01;
						case 0x0001:
							return 0xA4;
						case 0x0002:
							return 0x00;
					}

					break;
				case 0xA0:
					break;
				case 0xF0:
					break;
			}

			return bank[addr | _bankOffset];
		}

		private void StateSet(int val)
		{
			_stateBits = val &= 0x87;
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
			_internalRomState = 0;
			DriveLightOn = _boardLed;
		}

		private void FlashCommandReset()
		{
			_internalRomState = 0xF0;
			_commandLatch55 = false;
			_commandLatchAa = false;
		}

		private void FlashCommandWrite(int addr, int val)
		{
			if (addr == 0x0555) // $8555
			{
				if (!_commandLatchAa)
				{
					if (val == 0xAA)
					{
						_commandLatch55 = true;
					}
				}
				else
				{
					// process EZF command
					_internalRomState = val;
				}
			}
			else if (addr == 0x02AA) // $82AA
			{
				if (_commandLatch55 && val == 0x55)
				{
					_commandLatchAa = true;
				}
				else
				{
					_commandLatch55 = false;
				}
			}
			else
			{
				_commandLatch55 = false;
				_commandLatchAa = false;
			}			
		}

		public override void Write8000(int addr, int val)
		{
			WriteInternal(addr, val & 0x1FFF, _banksA, flashCommandEnable: true);
		}

		public override void WriteA000(int addr, int val)
		{
			WriteInternal(addr, val & 0x1FFF, _banksB, flashCommandEnable: false);
		}

		private void WriteInternal(int addr, int val, int[] bank, bool flashCommandEnable)
		{
			if (pinGame || !pinExRom)
			{
				return;
			}

			if (val == 0xF0) // any address, resets flash
			{
				FlashCommandReset();
			}
			else if (_internalRomState != 0x00 && _internalRomState != 0xF0)
			{
				switch (_internalRomState)
				{
					case 0x10:
						if (_eraseMode)
						{
							_banksA.AsSpan().Fill(0xFF);
							_banksB.AsSpan().Fill(0xFF);
							_bankDeltaDirty = true;
							_saveRamDirty = true;
						}

						FlashCommandReset();
						break;
					case 0x30:
						if (_eraseMode)
						{
							var sector = _bankOffset;
							for (var i = 0; i < 8; i++)
							{
								bank.AsSpan(sector & ((64 << 13) - 1)).Fill(0xFF);
								sector += 0x2000;
							}
						}
						
						FlashCommandReset();
						break;
					case 0x80:
						_eraseMode = true;
						break;
					case 0xA0:
						bank[(addr & 0x1FFF) | _bankOffset] &= val;
						_bankDeltaDirty = true;
						_saveRamDirty = true;

						// Device is reset to read/reset mode after write.
						FlashCommandReset();
						break;
				}
			}
			else if (flashCommandEnable)
			{
				FlashCommandWrite(addr & 0x1FFF, val);
			}
		}

		public override void WriteDE00(int addr, int val)
		{
			addr &= 0x02;
			if (addr == 0x00)
			{
				BankSet(val);
			}
			else
			{
				StateSet(val);
			}
		}

		public override void WriteDF00(int addr, int val)
		{
			_ram[addr] = val & 0xFF;
		}

		private (int[] Bank, int Index) TranslateDomainAddress(int addr)
		{
			var bankNumber = addr >> 14;
			var bankData = (addr & 0x2000) == 0 ? _banksA : _banksB;
			var index = (addr & 0x1FFF) + (bankNumber << 13);

			return (Bank: bankData, Index: index);
		}

		public override IEnumerable<MemoryDomain> CreateMemoryDomains()
		{
			yield return new MemoryDomainDelegate(
				name: "EF ROM",
				size: 0x2000 * 2 * 64,
				endian: MemoryDomain.Endian.Little,
				peek: addr =>
				{
					var (data, i) = TranslateDomainAddress(unchecked((int) addr));
					return unchecked((byte) data[i]);
				},
				poke: (addr, val) =>
				{
					var (data, i) = TranslateDomainAddress(unchecked((int) addr));
					data[i] = val;
				},
				wordSize: 1
			);

			yield return new MemoryDomainDelegate(
				name: "EF RAM",
				size: _ram.Length,
				endian: MemoryDomain.Endian.Little,
				peek: a => unchecked((byte) _ram[a]),
				poke: (a, d) => _ram[a] = d,
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

		public void StoreSaveRam(byte[] data)
		{
			using var stream = new MemoryStream(data);
			using var reader = new BinaryReader(stream);
			
			var deltaASize = reader.ReadInt32();
			_deltaA = reader.ReadBytes(deltaASize);
			var deltaBSize = reader.ReadInt32();
			_deltaB = reader.ReadBytes(deltaBSize);

			DeltaSerializer.ApplyDelta<int>(_originalMediaA, _banksA, _deltaA);
			DeltaSerializer.ApplyDelta<int>(_originalMediaB, _banksB, _deltaB);
			_saveRamDirty = false;
		}

		public bool SaveRamModified => _saveRamDirty;
	}
}
