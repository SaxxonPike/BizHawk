using System.Collections.Generic;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64
{
	public partial class C64
	{
		private IMemoryDomains _memoryDomains;

		private void SetupMemoryDomains()
		{
			bool diskDriveEnabled = _board.DiskDrive != null;
			bool tapeDriveEnabled = _board.TapeDrive != null;
			bool cartEnabled = _board.CartPort.IsConnected;

			var domains = new List<MemoryDomain>
			{
				C64MemoryDomainFactory.Create(
					name: "System Bus", 
					size: 0x10000, 
					peekByte: _board.Cpu.Peek, 
					pokeByte: _board.Cpu.Poke
				),
				C64MemoryDomainFactory.Create(
					name: "RAM", 
					size: 0x10000, 
					peekByte: _board.Ram.Peek, 
					pokeByte: _board.Ram.Poke
				),
				C64MemoryDomainFactory.Create(
					name: "CIA0", 
					size: 0x10, 
					peekByte: _board.Cia0.Peek, 
					pokeByte: _board.Cia0.Poke
				),
				C64MemoryDomainFactory.Create(
					name: "CIA1", 
					size: 0x10, 
					peekByte: _board.Cia1.Peek, 
					pokeByte: _board.Cia1.Poke
				),
				C64MemoryDomainFactory.Create(
					name: "VIC", 
					size: 0x40, 
					peekByte: _board.Vic.Peek, 
					pokeByte: _board.Vic.Poke
				),
				C64MemoryDomainFactory.Create(
					name: "SID", 
					size: 0x20, 
					peekByte: _board.Sid.Peek, 
					pokeByte: _board.Sid.Poke
				)
			};

			if (diskDriveEnabled)
			{
				domains.AddRange(new[]
				{
					C64MemoryDomainFactory.Create(
						name: "1541 Bus",
						size: 0x10000, 
						peekByte: _board.DiskDrive.Peek,
						pokeByte: _board.DiskDrive.Poke
					),
					C64MemoryDomainFactory.Create(
						name: "1541 RAM",
						size: 0x800, 
						peekByte: _board.DiskDrive.Peek,
						pokeByte: _board.DiskDrive.Poke
					),
					C64MemoryDomainFactory.Create(
						name: "1541 VIA0",
						size: 0x10, 
						peekByte: _board.DiskDrive.PeekVia0,
						pokeByte: _board.DiskDrive.PokeVia0
					),
					C64MemoryDomainFactory.Create(
						name: "1541 VIA1",
						size: 0x10, 
						peekByte: _board.DiskDrive.PeekVia1,
						pokeByte: _board.DiskDrive.PokeVia1
					)
				});
			}

			if (tapeDriveEnabled && (_board.TapeDrive.TapeDataDomain != null))
			{
				domains.AddRange(new[]
				{
					C64MemoryDomainFactory.Create("Tape Data", _board.TapeDrive.TapeDataDomain.Length, a => _board.TapeDrive.TapeDataDomain[a], (a, v) => _board.TapeDrive.TapeDataDomain[a] = (byte)v)
				});
			}

			if (cartEnabled)
			{
				domains.AddRange(_board.CartPort.CreateMemoryDomains());
			}

			_memoryDomains = new MemoryDomainList(domains);
			((BasicServiceProvider)ServiceProvider).Register(_memoryDomains);
		}

		private static class C64MemoryDomainFactory
		{
			public static MemoryDomain Create(string name, int size, Func<ushort, byte> peekByte, Action<ushort, byte> pokeByte)
			{
				return new MemoryDomainDelegate(
					name: name, 
					size: size,
					endian: MemoryDomain.Endian.Little,
					peek: addr => peekByte(unchecked((ushort)addr)),
					poke: (addr, val) => pokeByte(unchecked((ushort)addr), val), 1);
			}
		}
	}
}
