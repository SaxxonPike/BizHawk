using System.Collections.Generic;
using System.IO;
using System.Linq;
using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge
{
	public abstract class CartridgeDevice : IDriveLight
	{
		public Func<int> ReadOpenBus;

		public static CartridgeDevice Load(byte[] crtFile)
		{
			using var mem = new MemoryStream(crtFile);
			var reader = new BinaryReader(mem);

			if (new string(reader.ReadChars(16)) != "C64 CARTRIDGE   ")
			{
				return null;
			}

			var chipAddress = new List<int>();
			var chipBank = new List<int>();
			var chipData = new List<byte[]>();
			var chipType = new List<int>();

			var headerLength = ReadCRTInt(reader);
			var version = ReadCRTShort(reader);
			var mapper = ReadCRTShort(reader);
			var exrom = reader.ReadByte() != 0;
			var game = reader.ReadByte() != 0;

			// reserved
			reader.ReadBytes(6);

			// cartridge name
			reader.ReadBytes(0x20);

			// skip extra header bytes
			if (headerLength > 0x40)
			{
				reader.ReadBytes(headerLength - 0x40);
			}

			// read chips
			while (reader.PeekChar() >= 0)
			{
				if (new string(reader.ReadChars(4)) != "CHIP")
				{
					break;
				}

				var chipLength = ReadCRTInt(reader);
				chipType.Add(ReadCRTShort(reader));
				chipBank.Add(ReadCRTShort(reader));
				chipAddress.Add(ReadCRTShort(reader));
				var chipDataLength = ReadCRTShort(reader);
				chipData.Add(reader.ReadBytes(chipDataLength));
				chipLength -= chipDataLength + 0x10;
				if (chipLength > 0)
				{
					reader.ReadBytes(chipLength);
				}
			}

			if (chipData.Count <= 0)
			{
				return null;
			}

			var chips = BuildChipList(chipAddress, chipBank, chipData);
			CartridgeDevice result;
			switch (mapper)
			{
				case 0x0000:    // Standard Cartridge
					result = new Mapper0000(chips, game, exrom);
					break;
				case 0x0001:    // Action Replay (4.2 and up)
					result = new Mapper0001(chips);
					break;
				case 0x0005:    // Ocean
					result = new Mapper0005(chips);
					break;
				case 0x0007:    // Fun Play
					result = new Mapper0007(chips, game, exrom);
					break;
				case 0x0008:    // SuperGame
					result = new Mapper0008(chips);
					break;
				case 0x000A:    // Epyx FastLoad
					result = new Mapper000A(chips);
					break;
				case 0x000B:    // Westermann Learning
					result = new Mapper000B(chips);
					break;
				case 0x000F:    // C64 Game System / System 3
					result = new Mapper000F(chips);
					break;
				case 0x0011:    // Dinamic
					result = new Mapper0011(chips);
					break;
				case 0x0012:    // Zaxxon / Super Zaxxon
					result = new Mapper0012(chips);
					break;
				case 0x0013:    // Domark
					result = new Mapper0013(chips);
					break;
				case 0x0020:    // EasyFlash
					result = new Mapper0020(chips);
					break;
				case 0x002B:    // Prophet 64
					result = new Mapper002B(chips);
					break;
				default:
					throw new Exception("This cartridge file uses an unrecognized mapper: " + mapper);
			}
			result.HardReset();

			return result;
		}

		private static List<CartridgeChip> BuildChipList(IList<int> addresses, IList<int> banks, IList<byte[]> data) =>
			Enumerable.Range(0, addresses.Count)
				.Select(i => new CartridgeChip
				{
					Address = addresses[i],
					Bank = banks[i],
					Data = data[i]
				})
				.ToList();

		private static int ReadCRTShort(BinaryReader reader)
		{
			return (reader.ReadByte() << 8) |
				reader.ReadByte();
		}

		private static int ReadCRTInt(BinaryReader reader)
		{
			return (reader.ReadByte() << 24) |
				(reader.ReadByte() << 16) |
				(reader.ReadByte() << 8) |
				reader.ReadByte();
		}

		protected bool pinExRom;

		protected bool pinGame;

		protected bool pinIRQ;

		protected bool pinNMI;

		protected bool pinReset;

		protected bool validCartridge;

		public virtual void ExecutePhase()
		{
		}

		public bool ExRom => pinExRom;

		public bool Game => pinGame;

		public virtual void HardReset()
		{
			pinIRQ = true;
			pinNMI = true;
			pinReset = true;
		}

		public bool IRQ => pinIRQ;

		public bool NMI => pinNMI;

		public virtual int Peek8000(int addr)
		{
			return ReadOpenBus();
		}

		public virtual int PeekA000(int addr)
		{
			return ReadOpenBus();
		}

		public virtual int PeekDE00(int addr)
		{
			return ReadOpenBus();
		}

		public virtual int PeekDF00(int addr)
		{
			return ReadOpenBus();
		}

		public virtual void Poke8000(int addr, int val)
		{
		}

		public virtual void PokeA000(int addr, int val)
		{
		}

		public virtual void PokeDE00(int addr, int val)
		{
		}

		public virtual void PokeDF00(int addr, int val)
		{
		}

		public virtual int Read8000(int addr)
		{
			return ReadOpenBus();
		}

		public virtual int ReadA000(int addr)
		{
			return ReadOpenBus();
		}

		public virtual int ReadDE00(int addr)
		{
			return ReadOpenBus();
		}

		public virtual int ReadDF00(int addr)
		{
			return ReadOpenBus();
		}

		public bool Reset => pinReset;

		protected abstract void SyncStateInternal(Serializer ser);

		public void SyncState(Serializer ser)
		{
			ser.Sync(nameof(pinExRom), ref pinExRom);
			ser.Sync(nameof(pinGame), ref pinGame);
			ser.Sync(nameof(pinIRQ), ref pinIRQ);
			ser.Sync(nameof(pinNMI), ref pinNMI);
			ser.Sync(nameof(pinReset), ref pinReset);

			ser.Sync(nameof(_driveLightEnabled), ref _driveLightEnabled);
			ser.Sync(nameof(_driveLightOn), ref _driveLightOn);

			SyncStateInternal(ser);
		}

		public bool Valid => validCartridge;

		public virtual void Write8000(int addr, int val)
		{
		}

		public virtual void WriteA000(int addr, int val)
		{
		}

		public virtual void WriteDE00(int addr, int val)
		{
		}

		public virtual void WriteDF00(int addr, int val)
		{
		}

		protected Dictionary<int, (byte Mask, byte[][] Data)> LoadRomBanks(IEnumerable<CartridgeChip> chips)
		{
			const int bankSize = 0x2000;
			const byte dummyData = 0xFF;
			var blocks = new Dictionary<int, (byte Mask, byte[][] Data)>();

			// This bank will be chosen if uninitialized.
			var dummyBank = new byte[bankSize];
			dummyBank.AsSpan().Fill(dummyData);

			// Load in each bank.
			foreach (var chip in chips)
			{
				var address = 0x8000 | (chip.Address & 0x3FFF);
				if (!blocks.TryGetValue(address, out var block))
				{
					block = blocks[address] = (Mask: 0x00, Data: new byte[256][]);
					block.Data.AsSpan().Fill(dummyBank);
				}
				
				// Bank wrap-around is based on powers of 2.
				var bankMask = block.Mask;
				while (chip.Bank > bankMask)
				{
					bankMask = unchecked((byte) ((bankMask << 1) | 1));
				}

				var bank = new byte[bankSize];
				bank.AsSpan().Fill(dummyData);
				chip.ConvertDataToBytes().CopyTo(bank.AsSpan());
				block.Data[chip.Bank] = bank;
				blocks[address] = block with { Mask = bankMask };
			}
			
			return blocks;
		}

		public virtual IEnumerable<MemoryDomain> CreateMemoryDomains() => 
			Array.Empty<MemoryDomain>();

		private bool _driveLightEnabled;
		private bool _driveLightOn;

		public bool DriveLightEnabled
		{
			get => _driveLightEnabled;
			protected set => _driveLightEnabled = value;
		}

		public bool DriveLightOn
		{
			get => _driveLightOn;
			protected set => _driveLightOn = value;
		}

		public string DriveLightIconDescription => "Cart Activity";
	}
}
