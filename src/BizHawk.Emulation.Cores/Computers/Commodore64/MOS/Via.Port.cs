using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	public sealed partial class Via
	{
		private interface IPort
		{
			byte ReadPra(byte pra, byte ddra);
			byte ReadPrb(byte prb, byte ddrb);
			byte ReadExternalPra();
			byte ReadExternalPrb();

			void SyncState(Serializer ser);
		}

		private sealed class DisconnectedPort : IPort
		{
			public byte ReadPra(byte pra, byte ddra)
			{
				return unchecked((byte)(pra | ~ddra));
			}

			public byte ReadPrb(byte prb, byte ddrb)
			{
				return unchecked((byte)(prb | ~ddrb));
			}

			public byte ReadExternalPra()
			{
				return 0xFF;
			}

			public byte ReadExternalPrb()
			{
				return 0xFF;
			}

			public void SyncState(Serializer ser)
			{
				// Do nothing
			}
		}

		private sealed class DriverPort : IPort
		{
			private readonly Func<byte> _readPrA;
			private readonly Func<byte> _readPrB;

			public DriverPort(Func<byte> readPrA, Func<byte> readPrB)
			{
				_readPrA = readPrA;
				_readPrB = readPrB;
			}

			public byte ReadPra(byte pra, byte ddra)
			{
				return unchecked((byte) ((pra | ~ddra) & ReadExternalPra()));
			}

			public byte ReadPrb(byte prb, byte ddrb)
			{
				return unchecked((byte) ((prb & ddrb) | (_readPrB() & ~ddrb)));
			}

			public byte ReadExternalPra()
			{
				return _readPrA();
			}

			public byte ReadExternalPrb()
			{
				return _readPrB();
			}

			public void SyncState(Serializer ser)
			{
				// Do nothing
			}
		}

		private sealed class IecPort : IPort
		{
			private readonly Func<bool> _readClock;
			private readonly Func<bool> _readData;
			private readonly Func<bool> _readAtn;

			private int _driveNumber;

			public IecPort(Func<bool> readClock, Func<bool> readData, Func<bool> readAtn, int driveNumber)
			{
				_readClock = readClock;
				_readData = readData;
				_readAtn = readAtn;
				_driveNumber = (driveNumber & 0x3) << 5;
			}

			public byte ReadPra(byte pra, byte ddra)
			{
				return unchecked((byte)((pra | ~ddra) & ReadExternalPra()));
			}

			public byte ReadPrb(byte prb, byte ddrb)
			{
				return unchecked((byte) ((prb & ddrb) |
					(~ddrb & 0xE5 & (
						(_readClock() ? 0x04 : 0x00) |
						(_readData() ? 0x01 : 0x00) |
						(_readAtn() ? 0x80 : 0x00) |
						_driveNumber))));
			}

			public byte ReadExternalPra()
			{
				return 0xFF;
			}

			public byte ReadExternalPrb()
			{
				return unchecked((byte) (
					(_readClock() ? 0x04 : 0x00) |
					(_readData() ? 0x01 : 0x00) |
					(_readAtn() ? 0x80 : 0x00) |
					_driveNumber));
			}

			public void SyncState(Serializer ser)
			{
				ser.Sync(nameof(_driveNumber), ref _driveNumber);
			}
		}
	}
}
