using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

public class ReuMapper : CartridgeDevice
{
	private int _systemAddress;
	private int _reuAddress;
	private bool _dma;

	protected override void SyncStateInternal(Serializer ser)
	{
	}

	public override int PeekDF00(int addr)
	{
	}

	public override void PokeDF00(int addr, int val)
	{
	}

	public override int ReadDF00(int addr)
	{
	}

	public override void WriteDF00(int addr, int val)
	{
	}

	private int ReadRegisterInternal(int addr)
	{
		
	}
}