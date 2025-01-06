namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

/// <summary>
/// Represents a single cartridge ROM or RAM chip in a cartridge image.
/// </summary>
public class CartridgeChip
{
	public int Address;
	public int Bank;
	public ReadOnlyMemory<byte> Data;
	public ReadOnlyMemory<byte> ExtraData;
	public CartridgeChipType Type;
}