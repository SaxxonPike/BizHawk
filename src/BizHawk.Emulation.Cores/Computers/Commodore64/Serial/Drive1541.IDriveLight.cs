using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Serial;

public sealed partial class Drive1541 : IDriveLight
{
	public bool DriveLightEnabled => true;

	public bool DriveLightOn => _ledEnabled;

	public string DriveLightIconDescription => "Disk Drive LED";
}