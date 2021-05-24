#pragma once

#include "stdafx.h"
#include "Shared/Interfaces/IControlManager.h"
#include "Utilities/SimpleLock.h"
#include "Utilities/ISerializable.h"

class BaseControlDevice;
class IInputRecorder;
class IInputProvider;
class Emulator;
struct ControllerData;
enum class ControllerType;

class BaseControlManager : public IControlManager
{
private:
	vector<IInputRecorder*> _inputRecorders;
	vector<IInputProvider*> _inputProviders;

protected:
	Emulator* _emu = nullptr;
	SimpleLock _deviceLock;
	vector<shared_ptr<BaseControlDevice>> _systemDevices;
	vector<shared_ptr<BaseControlDevice>> _controlDevices;
	uint32_t _pollCounter = 0;

	void RegisterControlDevice(shared_ptr<BaseControlDevice> controlDevice);

	void ClearDevices();

public:
	BaseControlManager(Emulator* emu);
	virtual ~BaseControlManager();

	void AddSystemControlDevice(shared_ptr<BaseControlDevice> device) override;

	void UpdateControlDevices() override {}
	void UpdateInputState() override;

	bool HasControlDevice(ControllerType type) override;

	uint32_t GetPollCounter() override;
	void SetPollCounter(uint32_t value) override;

	virtual void Reset(bool softReset) {}

	void RegisterInputProvider(IInputProvider* provider) override;
	void UnregisterInputProvider(IInputProvider* provider) override;

	void RegisterInputRecorder(IInputRecorder* recorder) override;
	void UnregisterInputRecorder(IInputRecorder* recorder) override;

	shared_ptr<BaseControlDevice> CreateControllerDevice(ControllerType type, uint8_t port) override;

	vector<ControllerData> GetPortStates();

	shared_ptr<BaseControlDevice> GetControlDevice(uint8_t port) override;
	vector<shared_ptr<BaseControlDevice>> GetControlDevices();
};
