using System.Collections.Generic;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge
{
	internal sealed class Mapper0000 : CartridgeDevice
	{
		private readonly byte[] _romA;
		private int _romAMask;

		private readonly byte[] _romB;
		private int _romBMask;

		// standard cartridge mapper (Commodore)
		// note that this format also covers Ultimax carts
		public Mapper0000(IReadOnlyList<CartridgeChip> chips, bool game, bool exrom)
		{
			pinGame = game;
			pinExRom = exrom;

			validCartridge = true;

			// default to empty banks
			_romA = new byte[1];
			_romB = new byte[1];
			_romA[0] = 0xFF;
			_romB[0] = 0xFF;

			foreach (var chip in chips)
			{
				if (chip.Address == 0x8000)
				{
					switch (chip.Data.Length)
					{
						case 0x1000:
							_romAMask = 0x0FFF;
							_romA = chip.ConvertDataToBytes();
							break;
						case 0x2000:
							_romAMask = 0x1FFF;
							_romA = chip.ConvertDataToBytes();
							break;
						case 0x4000:
							_romAMask = 0x1FFF;
							_romBMask = 0x1FFF;

							// split the rom into two banks
							_romA = new byte[0x2000];
							_romB = new byte[0x2000];
							chip.ConvertDataToBytes().AsSpan(0x0000, 0x2000).CopyTo(_romA.AsSpan());
							chip.ConvertDataToBytes().AsSpan(0x2000, 0x2000).CopyTo(_romB.AsSpan());
							break;
						default:
							validCartridge = false;
							return;
					}
				}
				else if (chip.Address == 0xA000 || chip.Address == 0xE000)
				{
					switch (chip.Data.Length)
					{
						case 0x1000:
							_romBMask = 0x0FFF;
							break;
						case 0x2000:
							_romBMask = 0x1FFF;
							break;
						default:
							validCartridge = false;
							return;
					}

					_romB = chip.ConvertDataToBytes();
				}
			}
		}

		protected override void SyncStateInternal(Serializer ser)
		{
			ser.Sync("RomMaskA", ref _romAMask);
			ser.Sync("RomMaskB", ref _romBMask);
		}

		public override int Peek8000(int addr)
		{
			return _romA[addr & _romAMask];
		}

		public override int PeekA000(int addr)
		{
			return _romB[addr & _romBMask];
		}

		public override int Read8000(int addr)
		{
			return _romA[addr & _romAMask];
		}

		public override int ReadA000(int addr)
		{
			return _romB[addr & _romBMask];
		}
	}
}
