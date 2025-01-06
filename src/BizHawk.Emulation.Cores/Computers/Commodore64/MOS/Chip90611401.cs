using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	// emulates the PLA
	// which handles all bank switching
	public sealed class Chip90611401
	{
		// ------------------------------------
		public Func<ushort, byte> PeekBasicRom;
		public Func<ushort, byte> PeekCartridgeLo;
		public Func<ushort, byte> PeekCartridgeHi;
		public Func<ushort, byte> PeekCharRom;
		public Func<ushort, byte> PeekCia0;
		public Func<ushort, byte> PeekCia1;
		public Func<ushort, byte> PeekColorRam;
		public Func<ushort, byte> PeekExpansionLo;
		public Func<ushort, byte> PeekExpansionHi;
		public Func<ushort, byte> PeekKernalRom;
		public Func<ushort, byte> PeekMemory;
		public Func<ushort, byte> PeekSid;
		public Func<ushort, byte> PeekVic;
		public Action<ushort, byte> PokeCartridgeLo;
		public Action<ushort, byte> PokeCartridgeHi;
		public Action<ushort, byte> PokeCia0;
		public Action<ushort, byte> PokeCia1;
		public Action<ushort, byte> PokeColorRam;
		public Action<ushort, byte> PokeExpansionLo;
		public Action<ushort, byte> PokeExpansionHi;
		public Action<ushort, byte> PokeMemory;
		public Action<ushort, byte> PokeSid;
		public Action<ushort, byte> PokeVic;
		public Func<ushort, byte> ReadBasicRom;
		public Func<ushort, byte> ReadCartridgeLo;
		public Func<ushort, byte> ReadCartridgeHi;
		public Func<bool> ReadCharen;
		public Func<ushort, byte> ReadCharRom;
		public Func<ushort, byte> ReadCia0;
		public Func<ushort, byte> ReadCia1;
		public Func<ushort, byte> ReadColorRam;
		public Func<ushort, byte> ReadExpansionLo;
		public Func<ushort, byte> ReadExpansionHi;
		public Func<bool> ReadExRom;
		public Func<bool> ReadGame;
		public Func<bool> ReadHiRam;
		public Func<ushort, byte> ReadKernalRom;
		public Func<bool> ReadLoRam;
		public Func<ushort, byte> ReadMemory;
		public Func<ushort, byte> ReadSid;
		public Func<ushort, byte> ReadVic;
		public Action<ushort, byte> WriteCartridgeLo;
		public Action<ushort, byte> WriteCartridgeHi;
		public Action<ushort, byte> WriteCia0;
		public Action<ushort, byte> WriteCia1;
		public Action<ushort, byte> WriteColorRam;
		public Action<ushort, byte> WriteExpansionLo;
		public Action<ushort, byte> WriteExpansionHi;
		public Action<ushort, byte> WriteMemory;
		public Action<ushort, byte> WriteSid;
		public Action<ushort, byte> WriteVic;

		// ------------------------------------
		private enum PlaBank
		{
			None,
			Ram,
			BasicRom,
			KernalRom,
			CharRom,
			CartridgeLo,
			CartridgeHi,
			Vic,
			Sid,
			ColorRam,
			Cia0,
			Cia1,
			Expansion0,
			Expansion1
		}

		// ------------------------------------
		private bool _p24;
		private bool _p25;
		private bool _p26;
		private bool _p27;
		private bool _p28;
		private bool _loram;
		private bool _hiram;
		private bool _game;
		private bool _exrom;
		private bool _charen;
		private bool _a15;
		private bool _a14;
		private bool _a13;
		private bool _a12;

		private PlaBank Bank(ushort addr, bool read)
		{
			_loram = ReadLoRam();
			_hiram = ReadHiRam();
			_game = ReadGame();

			_a15 = (addr & 0x08000) != 0;
			_a14 = (addr & 0x04000) != 0;
			_a13 = (addr & 0x02000) != 0;
			_a12 = (addr & 0x01000) != 0;

			// upper memory regions 8000-FFFF
			_exrom = ReadExRom();
			if (_a15)
			{
				// io/character access
				if (_a14 && !_a13 && _a12)
				{
					// character rom, banked in at D000-DFFF
					_charen = ReadCharen();
					if (read && !_charen && (((_hiram || _loram) && _game) || (_hiram && !_exrom && !_game)))
					{
						return PlaBank.CharRom;
					}

					// io block, banked in at D000-DFFF
					if ((_charen && (_hiram || _loram)) || (_exrom && !_game))
					{
						if (addr < 0xD400)
						{
							return PlaBank.Vic;
						}

						if (addr < 0xD800)
						{
							return PlaBank.Sid;
						}

						if (addr < 0xDC00)
						{
							return PlaBank.ColorRam;
						}

						if (addr < 0xDD00)
						{
							return PlaBank.Cia0;
						}

						if (addr < 0xDE00)
						{
							return PlaBank.Cia1;
						}

						return addr < 0xDF00
							? PlaBank.Expansion0
							: PlaBank.Expansion1;
					}
				}

				// cartridge high, banked either at A000-BFFF or E000-FFFF depending
				if (_a13 && !_game && ((_hiram && !_a14 && read && !_exrom) || (_a14 && _exrom)))
				{
					return PlaBank.CartridgeHi;
				}

				// cartridge low, banked at 8000-9FFF
				if (!_a14 && !_a13 && ((_loram && _hiram && read && !_exrom) || (_exrom && !_game)))
				{
					return PlaBank.CartridgeLo;
				}

				// kernal rom, banked at E000-FFFF
				if (_hiram && _a14 && _a13 && read && (_game || (!_exrom && !_game)))
				{
					return PlaBank.KernalRom;
				}

				// basic rom, banked at A000-BFFF
				if (_loram && _hiram && !_a14 && _a13 && read && _game)
				{
					return PlaBank.BasicRom;
				}
			}

			// ultimax mode ram exclusion
			if (_exrom && !_game)
			{
				_p24 = !_a15 && !_a14 && _a12;         // 00x1 1000-1FFF, 3000-3FFF
				_p25 = !_a15 && !_a14 && _a13;         // 001x 2000-3FFF
				_p26 = !_a15 && _a14;                  // 01xx 4000-7FFF
				_p27 = _a15 && !_a14 && _a13;          // 101x A000-BFFF
				_p28 = _a15 && _a14 && !_a13 && !_a12; // 1100 C000-CFFF
				if (_p24 || _p25 || _p26 || _p27 || _p28)
				{
					return PlaBank.None;
				}
			}

			return PlaBank.Ram;
		}

		public byte Peek(ushort addr)
		{
			switch (Bank(addr, true))
			{
				case PlaBank.BasicRom:
					return PeekBasicRom(addr);
				case PlaBank.CartridgeHi:
					return PeekCartridgeHi(addr);
				case PlaBank.CartridgeLo:
					return PeekCartridgeLo(addr);
				case PlaBank.CharRom:
					return PeekCharRom(addr);
				case PlaBank.Cia0:
					return PeekCia0(addr);
				case PlaBank.Cia1:
					return PeekCia1(addr);
				case PlaBank.ColorRam:
					return PeekColorRam(addr);
				case PlaBank.Expansion0:
					return PeekExpansionLo(addr);
				case PlaBank.Expansion1:
					return PeekExpansionHi(addr);
				case PlaBank.KernalRom:
					return PeekKernalRom(addr);
				case PlaBank.Ram:
					return PeekMemory(addr);
				case PlaBank.Sid:
					return PeekSid(addr);
				case PlaBank.Vic:
					return PeekVic(addr);
			}

			return 0xFF;
		}

		public void Poke(ushort addr, byte val)
		{
			switch (Bank(addr, false))
			{
				case PlaBank.CartridgeHi:
					PokeCartridgeHi(addr, val);
					break;
				case PlaBank.CartridgeLo:
					PokeCartridgeLo(addr, val);
					break;
				case PlaBank.Cia0:
					PokeCia0(addr, val);
					break;
				case PlaBank.Cia1:
					PokeCia1(addr, val);
					break;
				case PlaBank.ColorRam:
					PokeColorRam(addr, val);
					break;
				case PlaBank.Expansion0:
					PokeExpansionLo(addr, val);
					break;
				case PlaBank.Expansion1:
					PokeExpansionHi(addr, val);
					break;
				case PlaBank.Ram:
					PokeMemory(addr, val);
					break;
				case PlaBank.Sid:
					PokeSid(addr, val);
					break;
				case PlaBank.Vic:
					PokeVic(addr, val);
					break;
			}
		}

		public byte Read(ushort addr)
		{
			switch (Bank(addr, true))
			{
				case PlaBank.BasicRom:
					return ReadBasicRom(addr);
				case PlaBank.CartridgeHi:
					return ReadCartridgeHi(addr);
				case PlaBank.CartridgeLo:
					return ReadCartridgeLo(addr);
				case PlaBank.CharRom:
					return ReadCharRom(addr);
				case PlaBank.Cia0:
					return ReadCia0(addr);
				case PlaBank.Cia1:
					return ReadCia1(addr);
				case PlaBank.ColorRam:
					return ReadColorRam(addr);
				case PlaBank.Expansion0:
					return ReadExpansionLo(addr);
				case PlaBank.Expansion1:
					return ReadExpansionHi(addr);
				case PlaBank.KernalRom:
					return ReadKernalRom(addr);
				case PlaBank.Ram:
					return ReadMemory(addr);
				case PlaBank.Sid:
					return ReadSid(addr);
				case PlaBank.Vic:
					return ReadVic(addr);
			}

			return 0xFF;
		}

		public void SyncState(Serializer ser)
		{
			ser.Sync(nameof(_p24), ref _p24);
			ser.Sync(nameof(_p25), ref _p25);
			ser.Sync(nameof(_p26), ref _p26);
			ser.Sync(nameof(_p27), ref _p27);
			ser.Sync(nameof(_p28), ref _p28);
			ser.Sync(nameof(_loram), ref _loram);
			ser.Sync(nameof(_hiram), ref _hiram);
			ser.Sync(nameof(_game), ref _game);
			ser.Sync(nameof(_exrom), ref _exrom);
			ser.Sync(nameof(_charen), ref _charen);
			ser.Sync(nameof(_a15), ref _a15);
			ser.Sync(nameof(_a14), ref _a14);
			ser.Sync(nameof(_a13), ref _a13);
			ser.Sync(nameof(_a12), ref _a12);
		}

		public byte VicRead(ushort addr)
		{
			_game = ReadGame();
			_exrom = ReadExRom();
			_a14 = (addr & 0x04000) == 0;
			_a13 = (addr & 0x02000) != 0;
			_a12 = (addr & 0x01000) != 0;

			// read char rom at 1000-1FFF and 9000-9FFF
			if (_a14 && !_a13 && _a12 && (_game || !_exrom))
			{
				return ReadCharRom(addr);
			}

			// read cartridge rom in ultimax mode
			if (_a13 && _a12 && _exrom && !_game)
			{
				return ReadCartridgeHi(addr);
			}

			return ReadMemory(addr);
		}

		public void Write(ushort addr, byte val)
		{
			switch (Bank(addr, false))
			{
				case PlaBank.CartridgeHi:
					WriteCartridgeHi(addr, val);
					if (ReadGame() || !ReadExRom())
					{
						WriteMemory(addr, val);
					}

					break;
				case PlaBank.CartridgeLo:
					WriteCartridgeLo(addr, val);
					if (ReadGame() || !ReadExRom())
					{
						WriteMemory(addr, val);
					}

					break;
				case PlaBank.Cia0:
					WriteCia0(addr, val);
					break;
				case PlaBank.Cia1:
					WriteCia1(addr, val);
					break;
				case PlaBank.ColorRam:
					WriteColorRam(addr, val);
					break;
				case PlaBank.Expansion0:
					WriteExpansionLo(addr, val);
					return;
				case PlaBank.Expansion1:
					WriteExpansionHi(addr, val);
					return;
				case PlaBank.Ram:
					WriteMemory(addr, val);
					break;
				case PlaBank.Sid:
					WriteSid(addr, val);
					break;
				case PlaBank.Vic:
					WriteVic(addr, val);
					break;
			}
		}
	}
}
