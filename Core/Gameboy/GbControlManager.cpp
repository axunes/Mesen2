#include "stdafx.h"
#include "Gameboy/GbControlManager.h"
#include "SNES/Input/SnesController.h"
#include "Shared/Emulator.h"
#include "Shared/EmuSettings.h"
#include "Shared/KeyManager.h"
#include "Shared/SystemActionManager.h"

GbControlManager::GbControlManager(Emulator* emu) : BaseControlManager(emu)
{
	_emu = emu;
}

shared_ptr<BaseControlDevice> GbControlManager::CreateControllerDevice(ControllerType type, uint8_t port)
{
	return nullptr;
}

void GbControlManager::UpdateControlDevices()
{
	auto lock = _deviceLock.AcquireSafe();
	
	ClearDevices();

	//TODO
	SnesConfig cfg = _emu->GetSettings()->GetSnesConfig();
	for(int i = 0; i < 2; i++) {
		shared_ptr<BaseControlDevice> device(new SnesController(_emu, 0, cfg.Controllers[0].Keys));
		if(device) {
			RegisterControlDevice(device);
		}
	}
}
