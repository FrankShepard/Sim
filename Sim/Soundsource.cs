using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sim
{
	public  class Soundsource
	{
		/// <summary>
		/// 与音源单片机通讯的命令
		/// </summary>
		public enum soundsource_cmd:byte
		{
			Cmd_EmergencyControl = 0x00,
			Cmd_PationOnoffControl = 0x01,
			Cmd_PationHiddenControl = 0x02,
			Cmd_PationOnoffQuery = 0x03,
			Cmd_PationHiddenQuery = 0x04,
			Cmd_PationErrorQuery = 0x05,
			Cmd_BeepErase = 0x06,
			Cmd_PationAllControl = 0x07,
			Cmd_PationErrorControl = 0x08,
			Cmd_FlashAddressSet = 0x09,
			Cmd_FlashDataWrite = 0x0A,
			Cmd_FlashDataRead = 0x0B,
			Cmd_WorkingStatusQuery = 0x0C,
			Cmd_PationAddressSet = 0x0D,
			Cmd_PationWatchRightsSet = 0x0E,
			Cmd_SelfCheck = 0x0F,
			Cmd_PationRegisterControl = 0x10,
			Cmd_PationRegisterQuery = 0x11,
			Cmd_SpeakerErrorQuery = 0x12,
			Cmd_SpeakerErrorControl = 0x13,
			Cmd_Reset = 0x14,
			Cmd_AmpStatusQuery = 0x15,
			Cmd_BaudrateControl = 0x16,
			Cmd_AmpRegisterControl = 0x1A,
			Cmd_AmpRegisterQuery = 0x1B,
			Cmd_AmpHiddenControl = 0x1C,
			Cmd_AmpHiddenQuery = 0x1D,
		}
	}
}
