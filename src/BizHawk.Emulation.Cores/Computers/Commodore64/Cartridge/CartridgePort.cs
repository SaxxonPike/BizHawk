using System.Collections.Generic;
using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

public sealed class CartridgePort
{
	public Func<byte> ReadOpenBus;

	private CartridgeDevice _cartridgeDevice;
	private bool _connected;

	public CartridgePort()
	{
		// start up with no media connected
		Disconnect();
	}

	internal string CartridgeType => _cartridgeDevice.GetType().Name;

	// ------------------------------------------

	public byte PeekHiExp(ushort addr)
	{
		return unchecked((byte) (_connected ? _cartridgeDevice.PeekDF00(unchecked((ushort) (addr & 0x00FF))) : 0xFF));
	}

	public byte PeekHiRom(ushort addr)
	{
		return unchecked((byte) (_connected ? _cartridgeDevice.PeekA000(unchecked((ushort) (addr & 0x1FFF))) : 0xFF));
	}

	public byte PeekLoExp(ushort addr)
	{
		return unchecked((byte) (_connected ? _cartridgeDevice.PeekDE00(unchecked((ushort) (addr & 0x00FF))) : 0xFF));
	}

	public byte PeekLoRom(ushort addr)
	{
		return unchecked((byte) (_connected ? _cartridgeDevice.Peek8000(unchecked((ushort) (addr & 0x1FFF))) : 0xFF));
	}

	public void PokeHiExp(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.PokeDF00(unchecked((ushort)(addr & 0x00FF)), val);
		}
	}

	public void PokeHiRom(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.PokeA000(unchecked((ushort)(addr & 0x1FFF)), val);
		}
	}
	public void PokeLoExp(ushort addr, byte val) 
	{
		if (_connected)
		{
			_cartridgeDevice.PokeDE00(unchecked((ushort)(addr & 0x00FF)), val);
		} 
	}

	public void PokeLoRom(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.Poke8000(unchecked((ushort)(addr & 0x1FFF)), val);
		}
	}

	public bool ReadExRom()
	{
		return !_connected || _cartridgeDevice.ExRom;
	}

	public bool ReadGame()
	{
		return !_connected || _cartridgeDevice.Game;
	}

	public byte ReadHiExp(ushort addr)
	{
		return unchecked((byte)(_connected ? _cartridgeDevice.ReadDF00(unchecked((ushort)(addr & 0x00FF))) : 0xFF));
	}

	public byte ReadHiRom(ushort addr)
	{
		return unchecked((byte)(_connected ? _cartridgeDevice.ReadA000(unchecked((ushort)(addr & 0x1FFF))) : 0xFF));
	}

	public byte ReadLoExp(ushort addr)
	{
		return unchecked((byte)(_connected ? _cartridgeDevice.ReadDE00(unchecked((ushort)(addr & 0x00FF))) : 0xFF));
	}

	public byte ReadLoRom(ushort addr)
	{
		return unchecked((byte)(_connected ? _cartridgeDevice.Read8000(unchecked((ushort)(addr & 0x1FFF))) : 0xFF));
	}

	public void WriteHiExp(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.WriteDF00(unchecked((ushort) (addr & 0x00FF)), val);
		}
	}

	public void WriteHiRom(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.WriteA000(unchecked((ushort)(addr & 0x1FFF)), val);
		}
	}

	public void WriteLoExp(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.WriteDE00(unchecked((ushort)(addr & 0x00FF)), val);
		}
	}

	public void WriteLoRom(ushort addr, byte val)
	{
		if (_connected)
		{
			_cartridgeDevice.Write8000(unchecked((ushort) (addr & 0x1FFF)), val);
		}
	}

	// ------------------------------------------

	public void Connect(CartridgeDevice newCartridgeDevice)
	{
		_connected = true;
		_cartridgeDevice = newCartridgeDevice;
		newCartridgeDevice.ReadOpenBus = ReadOpenBus;
	}

	public void Disconnect()
	{
		_cartridgeDevice = null;
		_connected = false;
	}

	public void ExecutePhase()
	{
		if (_connected)
		{
			_cartridgeDevice.ExecutePhase();
		}
	}

	public void HardReset()
	{
		// note: this will not disconnect any attached media
		if (_connected)
		{
			_cartridgeDevice.HardReset();
		}
	}

	public bool IsConnected => _connected;

	public bool ReadIrq()
	{
		return !_connected || _cartridgeDevice.IRQ;
	}

	public bool ReadNmi()
	{
		return !_connected || _cartridgeDevice.NMI;
	}

	public void SyncState(Serializer ser)
	{
		ser.Sync(nameof(_connected), ref _connected);

		ser.BeginSection(nameof(CartridgeDevice));
		_cartridgeDevice.SyncState(ser);
		ser.EndSection();
	}

	public ISaveRam SaveRam => _connected ? _cartridgeDevice as ISaveRam : null;
	
	public IDriveLight DriveLight => _connected ? _cartridgeDevice as IDriveLight : null;

	public IEnumerable<MemoryDomain> CreateMemoryDomains()
	{
		if (_connected)
		{
			return _cartridgeDevice.CreateMemoryDomains();
		}

		return Array.Empty<MemoryDomain>();
	}
}