using System.Collections.Generic;
using System.IO;
using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Computers.Commodore64.Media;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

public abstract class CartridgeDevice
{
	public Func<byte> ReadOpenBus;

	public static CartridgeDevice Load(byte[] crtFile)
	{
		using var mem = new MemoryStream(crtFile);
		var reader = new BinaryReader(mem);

		var config = CRT.ReadConfig(reader);
		if (config == null)
		{
			return null;
		}

		var chips = CRT.ReadChips(reader);
		if (chips.Count < 1)
		{
			return null;
		}

		var result = MapperFactory.Create(config, chips);
		if (result == null)
		{
			return null;
		}

		result.HardReset();
		return result;
	}

	protected bool pinExRom;

	protected bool pinGame;

	protected bool pinIRQ;

	protected bool pinNMI;

	protected bool pinReset;

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

	public virtual byte Peek8000(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual byte PeekA000(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual byte PeekDE00(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual byte PeekDF00(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual void Poke8000(ushort addr, byte val)
	{
	}

	public virtual void PokeA000(ushort addr, byte val)
	{
	}

	public virtual void PokeDE00(ushort addr, byte val)
	{
	}

	public virtual void PokeDF00(ushort addr, byte val)
	{
	}

	public virtual byte Read8000(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual byte ReadA000(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual byte ReadDE00(ushort addr)
	{
		return ReadOpenBus();
	}

	public virtual byte ReadDF00(ushort addr)
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

		SyncStateInternal(ser);
	}

	public virtual void Write8000(ushort addr, byte val)
	{
	}

	public virtual void WriteA000(ushort addr, byte val)
	{
	}

	public virtual void WriteDE00(ushort addr, byte val)
	{
	}

	public virtual void WriteDF00(ushort addr, byte val)
	{
	}

	/// <summary>
	/// Calculates the minimum bitmask required to cover the specified number of bytes.
	/// </summary>
	/// <param name="size">
	/// Number of bytes to cover with a bitmask.
	/// </param>
	protected static int GetMaskFromSize(int size)
	{
		var mask = 1;
		while (mask < size)
		{
			mask = (mask << 1) | 1;
		}

		return mask - 1;
	}
	
	/// <summary>
	/// Create a fixed size ROM image from this chip data.
	/// </summary>
	/// <param name="size">
	/// Size of the output image.
	/// </param>
	/// <param name="pad">
	/// Byte with which to fill any remaining ROM space.
	/// </param>
	/// <returns>
	/// ROM image, padded and limited to the specified size.
	/// </returns>
	protected static byte[] CreateRom(CartridgeChip chip, int size, byte pad = 0xFF)
	{
		var image = new byte[size];
		image.AsSpan()
			.Fill(pad);

		chip.Data.Span.Slice(0, Math.Min(size, chip.Data.Length)).CopyTo(image);
		return image;
	}

	/// <summary>
	/// Create a fixed size ROM image from this chip data. If the
	/// data is smaller than the specified image size, the bytes are
	/// repeated.
	/// </summary>
	/// <param name="size">
	/// Size of the output image.
	/// </param>
	/// <returns>
	/// ROM image, mirrored and limited to the specified size.
	/// </returns>
	protected static byte[] CreateMirroredRom(CartridgeChip chip, int size)
	{
		if (chip.Data.Length < 1)
		{
			throw new InvalidOperationException("Cannot mirror a ROM that is empty");
		}

		var image = new byte[size];
		var remaining = size;
		var offset = 0;

		while (remaining > 0)
		{
			chip.Data.Span.CopyTo(image.AsSpan(offset));
			offset += chip.Data.Length;
			remaining -= chip.Data.Length;
		}

		return image;
	}

	/// <summary>
	/// Map individual images to flat images based on starting address and bank number.
	/// </summary>
	/// <param name="chips">
	/// Chips with images to map.
	/// </param>
	/// <returns>
	/// A dictionary containing the bank mask and associated image banks.
	/// </returns>
	protected Dictionary<int, (byte Mask, byte[][] Data)> CreateRoms(IEnumerable<CartridgeChip> chips)
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
			chip.Data.Span.CopyTo(bank.AsSpan());
			block.Data[chip.Bank] = bank;
			blocks[address] = block with { Mask = bankMask };
		}
			
		return blocks;
	}

	public virtual IEnumerable<MemoryDomain> CreateMemoryDomains() => 
		Array.Empty<MemoryDomain>();
}