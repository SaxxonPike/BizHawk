namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

public class CartridgeConfig
{
	public bool GamePin { get; set; }
	public bool ExRomPin { get; set; }
	public int Version { get; set; }
	public string Name { get; set; }
	public ReadOnlyMemory<byte> ExtraData { get; set; }
	public int Mapper { get; set; }

}