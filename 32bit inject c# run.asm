_start:
	;we will get HeapAlloc, HeapFree, GetProcessHeap, LoadLibraryA and GetProcAddress off the stack thing (in that order)
	;eax will be an address that points to its starting
	mov eax, dword [esp+4]
	mov esi, eax; esi is our init varible storage
	
	call [esi+8];GetProcessHeap
	push 1024
	push 0x00000008
	push eax
	call [esi]; HeapAlloc
	mov ebx, eax;ebx is our varible storage of 1024 bytes
	
	
	
	mov eax, [esi+0]
	mov dword [ebx+300], eax
	mov eax, [esi+4]
	mov dword [ebx+304], eax
	mov eax, [esi+8]
	mov dword [ebx+308], eax
	mov eax, [esi+12]
	mov dword [ebx+312], eax
	mov eax, [esi+16]
	mov dword [ebx+316], eax
	
	mov eax, [esi+20]
	mov dword [ebx+400], eax
	mov eax, [esi+24]
	mov dword [ebx+404], eax
	
	mov eax, esi
	add eax, 32
	mov dword [ebx+200], eax
	
	mov eax, esi
	add eax, 32
	add eax, dword [esi+28];length of the NamespaceAndClassString
	add eax, 4;skip over the length of the next string
	mov dword [ebx+204], eax
	;[ebx+200] is the pointer to the Uninitialized NamespaceAndClassString
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+400] is the file pointer
	;[ebx+404] is the file length
	call _init
	cmp eax, 0
	je .ErrorOccured
	
	
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	
	
	mov dword [ebx+4], ebx
	call _GetCLSID_CLRMetaHost
	mov dword [ebx+8], eax
	call _GetIID_ICLRMetaHost
	mov dword [ebx+12], eax
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	push dword [ebx+4]
	push dword [ebx+12]
	push dword [ebx+8]
	call [ebx+352]
	cmp eax, dword 0
	jne .ErrorOccured
	;TODO: make sure eax is 0, do something on err
	
	mov eax, dword [ebx+0]
	mov eax, dword [eax]
	mov eax, [eax+20]
	mov dword [ebx+14], eax
	mov eax, ebx
	add eax, 18
	mov dword [ebx+22], eax
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	push dword [ebx+22]
	push dword [ebx+0]
	call [ebx+14]
	cmp eax, dword 0
	jne .ErrorOccured
	;TODO: make sure eax is 0, do something on err
	
	call _GetIID_ICLRRuntimeInfo
	mov dword [ebx+26], eax
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	
	mov eax, ebx
	add eax, 30
	mov dword [ebx+34], eax
	mov eax, ebx
	add eax, 38
	mov dword [ebx+42], eax
	mov eax, dword [ebx+18]
	mov eax, dword [eax]
	mov eax, dword [eax+12]
	mov dword [ebx+46], eax
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	.getLastestRuntimeLoop:
		push dword 0
		push dword [ebx+42]
		push dword 1
		push dword [ebx+18]
		call [ebx+46]
		cmp eax, dword 0
		jne .getLastestRuntimeLoopDone
		
		mov eax, dword [ebx+38]
		mov eax, dword [eax]
		mov eax, dword [eax+0]
		push dword [ebx+34]
		push dword [ebx+26]
		push dword [ebx+38]
		call eax
		jmp .getLastestRuntimeLoop
	
	.getLastestRuntimeLoopDone:
	
	cmp dword [ebx+30], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov eax, dword [ebx+30]
	mov eax, dword [eax]
	mov eax, dword [eax+36]
	mov dword [ebx+50], eax
	
	
	
	call _GetIID_ICorRuntimeHost
	mov [ebx+54],eax
	call _GetCLSID_CorRuntimeHost
	mov [ebx+58],eax
	mov eax, ebx
	add eax, 62
	mov [ebx+66], eax
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	push dword [ebx+66]
	push dword [ebx+54]
	push dword [ebx+58]
	push dword [ebx+30]
	call dword [ebx+50]
	cmp eax, dword 0
	jne .ErrorOccured
	;TODO: make sure eax is 0, do something on err
	
	cmp dword [ebx+62], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov eax, dword [ebx+62]
	mov eax, dword [eax]
	mov eax, dword [eax+40]
	mov dword [ebx+70], eax
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	push dword [ebx+62]
	call [ebx+70]
	cmp eax, 0
	jne .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov eax, dword [ebx+62]
	mov eax, dword [eax]
	mov eax, dword [eax+52]
	mov dword [ebx+74], eax
	
	mov eax, ebx
	add eax, 78
	mov dword [ebx+82], eax
	
	call _GetIID_AppDomain
	mov [ebx+86], eax
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+74] is the pointer to the GetDefaultDomain function
	;[ebx+78] is the pointer to appDomainThunk, aka *appDomainThunk
	;[ebx+82] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[ebx+86] is the potiner to IID_AppDomain
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	
	push dword [ebx+82]
	push dword [ebx+62]
	call [ebx+74]
	
	cmp dword [ebx+78], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	
	mov eax, ebx
	add eax, 90
	mov dword [ebx+94], eax
	
	mov eax, dword [ebx+78]
	mov eax, dword [eax]
	mov eax, dword [eax+0]
	mov dword [ebx+98], eax
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+74] is the pointer to the GetDefaultDomain function
	;[ebx+78] is the pointer to appDomainThunk, aka *appDomainThunk
	;[ebx+82] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[ebx+86] is the potiner to IID_AppDomain
	;[ebx+90] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[ebx+94] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[ebx+98] is the function QueryInterface
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	
	
	push dword [ebx+94]
	push dword [ebx+86]
	push dword [ebx+78]
	call [ebx+98]
	
	cmp dword [ebx+90], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov eax, [ebx+400]
	mov dword [ebx+102], eax
	mov eax, [ebx+404]
	mov dword [ebx+106], eax
	
	push 8
	call _malloc
	mov [ebx+110], eax
	
	mov eax, dword [ebx+110]
	mov edi, [ebx+106]
	mov dword [eax], edi
	mov dword [eax+4], 0
	
	
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+74] is the pointer to the GetDefaultDomain function
	;[ebx+78] is the pointer to appDomainThunk, aka *appDomainThunk
	;[ebx+82] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[ebx+86] is the potiner to IID_AppDomain
	;[ebx+90] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[ebx+94] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[ebx+98] is the function QueryInterface
	;[ebx+102] is the pointer to the file bytes
	;[ebx+106] is the file bytes length
	;[ebx+110] is the pointer to bounds
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	
	
	
	push dword [ebx+110]
	push dword 1
	push dword 17
	call [ebx+328]
	mov dword [ebx+114], eax
	
	push dword [ebx+114]
	call [ebx+332]
	
	mov eax, [ebx+114]
	add eax, 12
	
	push dword [eax]
	push dword [ebx+102]
	push dword [ebx+106]
	call _memcopy
	
	push dword [ebx+114]
	call [ebx+336]
	
	mov eax, ebx
	add eax, 118
	mov dword [ebx+122], eax
	
	mov eax, dword [ebx+90]
	mov eax, dword [eax]
	mov eax, dword [eax+180]
	mov dword [ebx+126], eax
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+74] is the pointer to the GetDefaultDomain function
	;[ebx+78] is the pointer to appDomainThunk, aka *appDomainThunk
	;[ebx+82] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[ebx+86] is the potiner to IID_AppDomain
	;[ebx+90] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[ebx+94] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[ebx+98] is the function QueryInterface
	;[ebx+102] is the pointer to the file bytes
	;[ebx+106] is the file bytes length
	;[ebx+110] is the pointer to bounds, aka *bounds
	;[ebx+114] is the potiner to safeArray, aka *safeArray
	;[ebx+118] is the pointer to managedAssembly, aka *managedAssembly
	;[ebx+122] is the pointer to a pointer of managedAssembly, aka **managedAssembly
	;[ebx+126] is the potiner to the function Load_3
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	
	push dword [ebx+122]
	push dword [ebx+114]
	push dword [ebx+90]
	call [ebx+126]
	cmp eax, dword 0
	jne .ErrorOccured
	;TODO: make sure eax is 0, do something on err
	
	cmp dword [ebx+118], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	
	mov eax, ebx
	add eax, 130
	mov dword [ebx+134], eax
	
	call _GetNameSpaceAndClassName
	mov dword [ebx+138], eax
	
	mov eax, dword [ebx+118]
	mov eax, dword [eax]
	mov eax, dword [eax+68]
	mov dword [ebx+142], eax
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+74] is the pointer to the GetDefaultDomain function
	;[ebx+78] is the pointer to appDomainThunk, aka *appDomainThunk
	;[ebx+82] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[ebx+86] is the potiner to IID_AppDomain
	;[ebx+90] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[ebx+94] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[ebx+98] is the function QueryInterface
	;[ebx+102] is the pointer to the file bytes
	;[ebx+106] is the file bytes length
	;[ebx+110] is the pointer to bounds, aka *bounds
	;[ebx+114] is the potiner to safeArray, aka *safeArray
	;[ebx+118] is the pointer to managedAssembly, aka *managedAssembly
	;[ebx+122] is the pointer to a pointer of managedAssembly, aka **managedAssembly
	;[ebx+126] is the potiner to the function Load_3
	;[ebx+130] is the potiner to managedType, aka *managedType
	;[ebx+134] is the potiner to a pointer of managedType, aka **managedType
	;[ebx+138] is the pointer to the NamespaceAndClassString
	;[ebx+142] is the pointer to the function GetType_2
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	
	push dword [ebx+134]
	push dword [ebx+138]
	push dword [ebx+118]
	call [ebx+142]
	
	cmp eax, dword 0
	jne .ErrorOccured
	;TODO: make sure eax is 0, do something on err
	
	cmp dword [ebx+130], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	push 0;zero arguments
	push 0
	push 12
	call [ebx+344]
	mov dword [ebx+146], eax
	
	call _CreateVariant
	mov dword [ebx+150], eax
	
	call _CreateVariant
	mov dword [ebx+154], eax
	
	mov eax, dword [ebx+130]
	mov eax, dword [eax]
	mov eax, dword [eax+228]
	mov dword [ebx+158], eax
	
	call _GetFunctionName
	mov dword [ebx+162], eax
	
	
	;[ebx+0] is the pointer to metaHost aka *metahost
	;[ebx+4] is the pointer to a pointer of metaHost aka **metahost
	;[ebx+8] is the pointer to CLSID_CLRMetaHost
	;[ebx+12] is the pointer to IID_ICLRMetaHost
	;[ebx+14] holds EnumerateInstalledRuntimes function
	;[ebx+18] holds the pointer to runtime, aka *runtime
	;[ebx+22] holds the pointer to a pointer of runtime, aka **runtime
	;[ebx+26] is the pointer to IID_ICLRRuntimeInfo
	;[ebx+30] is the pointer to runtimeinfo, aka *runtimeinfo
	;[ebx+34] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[ebx+38] is the pointer to enumRuntime, aka *enumRuntime
	;[ebx+42] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[ebx+46] is the pointer to the function Next
	;[ebx+50] is the pointer to the function GetInterface
	;[ebx+54] is the pointer to IID_ICorRuntimeHost
	;[ebx+58] is the pointer to CLSID_CorRuntimeHost
	;[ebx+62] is the pointer to runtimeHost, aka *runtimeHost
	;[ebx+66] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[ebx+70] is the pointer to the start function
	;[ebx+74] is the pointer to the GetDefaultDomain function
	;[ebx+78] is the pointer to appDomainThunk, aka *appDomainThunk
	;[ebx+82] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[ebx+86] is the potiner to IID_AppDomain
	;[ebx+90] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[ebx+94] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[ebx+98] is the function QueryInterface
	;[ebx+102] is the pointer to the file bytes
	;[ebx+106] is the file bytes length
	;[ebx+110] is the pointer to bounds, aka *bounds
	;[ebx+114] is the potiner to safeArray, aka *safeArray
	;[ebx+118] is the pointer to managedAssembly, aka *managedAssembly
	;[ebx+122] is the pointer to a pointer of managedAssembly, aka **managedAssembly
	;[ebx+126] is the potiner to the function Load_3
	;[ebx+130] is the potiner to managedType, aka *managedType
	;[ebx+134] is the potiner to a pointer of managedType, aka **managedType
	;[ebx+138] is the pointer to the NamespaceAndClassString
	;[ebx+142] is the pointer to the function GetType_2
	;[ebx+146] is the pointer to managedArguments, aka *managedArguments
	;[ebx+150] is the potiner to managedReturnValue, aka *managedReturnValue
	;[ebx+154] is the potiner to empty, aka *empty
	;[ebx+158] is the pointer to the function InvokeMember_3
	;[ebx+162] is the pointer to the MethodName
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	push dword [ebx+150]
	push dword [ebx+146]

	mov eax, dword [ebx+154]

	push dword [eax+12];need to pass the entire VARIANT structure (16 bytes)
	push dword [eax+8]
	push dword [eax+4]
	push dword [eax]

	push dword 0
	push dword 280
	push dword [ebx+162]
	push dword [ebx+130]
	call [ebx+158]
	cmp eax, 0
	jne .ErrorOccured
	ret 4
	
	.ErrorOccured:
	mov eax, 0xffffffff
	ret 4

_init:;this is to load all the needed libraries
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	
	push 12
	call _malloc
	mov byte [eax], 'm'
	mov byte [eax+1], 's'
	mov byte [eax+2], 'c'
	mov byte [eax+3], 'o'
	mov byte [eax+4], 'r'
	mov byte [eax+5], 'e'
	mov byte [eax+6], 'e'
	mov byte [eax+7], '.'
	mov byte [eax+8], 'd'
	mov byte [eax+9], 'l'
	mov byte [eax+10], 'l'
	mov byte [eax+11], 0
	push eax
	push eax
	call [ebx+312]
	mov dword [ebx+320], eax
	pop eax
	push eax
	call _mallocFree
	push 13
	call _malloc
	mov byte [eax], 'o'
	mov byte [eax+1], 'l'
	mov byte [eax+2], 'e'
	mov byte [eax+3], 'a'
	mov byte [eax+4], 'u'
	mov byte [eax+5], 't'
	mov byte [eax+6], '3'
	mov byte [eax+7], '2'
	mov byte [eax+8], '.'
	mov byte [eax+9], 'd'
	mov byte [eax+10], 'l'
	mov byte [eax+11], 'l'
	mov byte [eax+12], 0
	push eax
	push eax
	call [ebx+312]
	mov dword [ebx+324], eax
	pop eax
	push eax
	call _mallocFree
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	
	push 16
	call _malloc
	mov byte [eax+0], 'S'
	mov byte [eax+1], 'a'
	mov byte [eax+2], 'f'
	mov byte [eax+3], 'e'
	mov byte [eax+4], 'A'
	mov byte [eax+5], 'r'
	mov byte [eax+6], 'r'
	mov byte [eax+7], 'a'
	mov byte [eax+8], 'y'
	mov byte [eax+9], 'C'
	mov byte [eax+10], 'r'
	mov byte [eax+11], 'e'
	mov byte [eax+12], 'a'
	mov byte [eax+13], 't'
	mov byte [eax+14], 'e'
	mov byte [eax+15], 0
	push eax
	push eax
	push dword [ebx+324]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+328], eax
	pop eax
	push eax
	call _mallocFree
	push 14
	call _malloc
	mov byte [eax+0], 'S'
	mov byte [eax+1], 'a'
	mov byte [eax+2], 'f'
	mov byte [eax+3], 'e'
	mov byte [eax+4], 'A'
	mov byte [eax+5], 'r'
	mov byte [eax+6], 'r'
	mov byte [eax+7], 'a'
	mov byte [eax+8], 'y'
	mov byte [eax+9], 'L'
	mov byte [eax+10], 'o'
	mov byte [eax+11], 'c'
	mov byte [eax+12], 'k'
	mov byte [eax+13], 0
	push eax
	push eax
	push dword [ebx+324]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+332], eax
	pop eax
	push eax
	call _mallocFree
	push 16
	call _malloc
	mov byte [eax+0], 'S'
	mov byte [eax+1], 'a'
	mov byte [eax+2], 'f'
	mov byte [eax+3], 'e'
	mov byte [eax+4], 'A'
	mov byte [eax+5], 'r'
	mov byte [eax+6], 'r'
	mov byte [eax+7], 'a'
	mov byte [eax+8], 'y'
	mov byte [eax+9], 'U'
	mov byte [eax+10], 'n'
	mov byte [eax+11], 'l'
	mov byte [eax+12], 'o'
	mov byte [eax+13], 'c'
	mov byte [eax+14], 'k'
	mov byte [eax+15], 0
	push eax
	push eax
	push dword [ebx+324]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+336], eax
	pop eax
	push eax
	call _mallocFree
	push 15
	call _malloc
	mov byte [eax+0], 'S'
	mov byte [eax+1], 'y'
	mov byte [eax+2], 's'
	mov byte [eax+3], 'A'
	mov byte [eax+4], 'l'
	mov byte [eax+5], 'l'
	mov byte [eax+6], 'o'
	mov byte [eax+7], 'c'
	mov byte [eax+8], 'S'
	mov byte [eax+9], 't'
	mov byte [eax+10], 'r'
	mov byte [eax+11], 'i'
	mov byte [eax+12], 'n'
	mov byte [eax+13], 'g'
	mov byte [eax+14], 0
	push eax
	push eax
	push dword [ebx+324]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+340], eax
	pop eax
	push eax
	call _mallocFree
	push 22
	call _malloc
	mov byte [eax+0], 'S'
	mov byte [eax+1], 'a'
	mov byte [eax+2], 'f'
	mov byte [eax+3], 'e'
	mov byte [eax+4], 'A'
	mov byte [eax+5], 'r'
	mov byte [eax+6], 'r'
	mov byte [eax+7], 'a'
	mov byte [eax+8], 'y'
	mov byte [eax+9], 'C'
	mov byte [eax+10], 'r'
	mov byte [eax+11], 'e'
	mov byte [eax+12], 'a'
	mov byte [eax+13], 't'
	mov byte [eax+14], 'e'
	mov byte [eax+15], 'V'
	mov byte [eax+16], 'e'
	mov byte [eax+17], 'c'
	mov byte [eax+18], 't'
	mov byte [eax+19], 'o'
	mov byte [eax+20], 'r'
	mov byte [eax+21], 0
	push eax
	push eax
	push dword [ebx+324]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+344], eax
	pop eax
	push eax
	call _mallocFree
	push 12
	call _malloc
	mov byte [eax+0], 'V'
	mov byte [eax+1], 'a'
	mov byte [eax+2], 'r'
	mov byte [eax+3], 'i'
	mov byte [eax+4], 'a'
	mov byte [eax+5], 'n'
	mov byte [eax+6], 't'
	mov byte [eax+7], 'I'
	mov byte [eax+8], 'n'
	mov byte [eax+9], 'i'
	mov byte [eax+10], 't'
	mov byte [eax+11], 0
	push eax
	push eax
	push dword [ebx+324]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+348], eax
	pop eax
	push eax
	call _mallocFree
	
	push 18;CLRCreateInstance
	call _malloc
	mov byte [eax+0], 'C'
	mov byte [eax+1], 'L'
	mov byte [eax+2], 'R'
	mov byte [eax+3], 'C'
	mov byte [eax+4], 'r'
	mov byte [eax+5], 'e'
	mov byte [eax+6], 'a'
	mov byte [eax+7], 't'
	mov byte [eax+8], 'e'
	mov byte [eax+9], 'I'
	mov byte [eax+10], 'n'
	mov byte [eax+11], 's'
	mov byte [eax+12], 't'
	mov byte [eax+13], 'a'
	mov byte [eax+14], 'n'
	mov byte [eax+15], 'c'
	mov byte [eax+16], 'e'
	mov byte [eax+17], 0
	push eax
	push eax
	push dword [ebx+320]
	call [ebx+316]
	cmp eax, 0
	je .InitErrorOccured
	mov dword [ebx+352], eax
	pop eax
	push eax
	call _mallocFree
	
	;[ebx+300] is the function HeapAlloc
	;[ebx+304] is the function HeapFree
	;[ebx+308] is the function GetProcessHeap
	;[ebx+312] is the function LoadLibraryA
	;[ebx+316] is the function GetProcAddress
	;[ebx+320] is the handle to mscoree.dll
	;[ebx+324] is the handle to oleaut32.dll
	;[ebx+328] is the function SafeArrayCreate
	;[ebx+332] is the function SafeArrayLock
	;[ebx+336] is the function SafeArrayUnlock
	;[ebx+340] is the function SysAllocString
	;[ebx+344] is the function SafeArrayCreateVector
	;[ebx+348] is the function VariantInit
	;[ebx+352] is the function CLRCreateInstance
	mov eax, 1
	ret

	.InitErrorOccured:
	mov eax, 0
	ret


_memcopy: ;stdcall
	;use:
	;push target
	;push source
	;push count
	;call _memcopy
	;returns nothing and changes no registers
    
    push edi
    push esi
	push ecx
	
	mov ecx, [esp+16]
	mov esi, [esp+20]
	mov edi, [esp+24]
	
    .copy_loop:
        mov al, [esi] 
        mov [edi], al 
        inc esi 
        inc edi
        dec ecx 
        jnz .copy_loop 

    pop ecx
	pop esi    
    pop edi     

    ret 12





_malloc: ;stdcall
	mov eax, [ESP+4];get the size off the push stack
	push ecx
	push edx
	mov ecx, eax
	call [ebx+308];GetProcessHeap
	;eax hold the heap handle
	;ecx holding the heap size
	push ecx
	push 0x00000008; this can be 0, but i think zeroing the memory would be safer
	push eax
	call [ebx+300];HeapAlloc
	pop edx
	pop ecx
	ret 4
_mallocFree: ; stdcall
	push ebp
	mov ebp, [ESP+8];get the handle the push stack
	call [ebx+308];GetProcessHeap
	push ebp
	push 0
	push eax
	call [ebx+304];HeapFree
	pop ebp
	ret 4

_GetCLSID_CLRMetaHost:
	push 16
	call _malloc
	mov [eax], dword 0x9280188d 
	mov [eax+4], word 0xe8e
	mov [eax+6], word 0x4867
	mov [eax+8], byte 0xb3
	mov [eax+9], byte 0xc
	mov [eax+10], byte 0x7f
	mov [eax+11], byte 0xa8
	mov [eax+12], byte 0x38
	mov [eax+13], byte 0x84
	mov [eax+14], byte 0xe8
	mov [eax+15], byte 0xde
	ret

_GetIID_ICLRMetaHost:
	push 16
	call _malloc
	mov [eax], dword 0xD332DB9E 
	mov [eax+4], word 0xB9B3
	mov [eax+6], word 0x4125
	mov [eax+8], byte 0x82
	mov [eax+9], byte 0x07
	mov [eax+10], byte 0xA1
	mov [eax+11], byte 0x48
	mov [eax+12], byte 0x84
	mov [eax+13], byte 0xF5
	mov [eax+14], byte 0x32
	mov [eax+15], byte 0x16
	ret

_GetIID_ICLRRuntimeInfo:
	push 16
	call _malloc
	mov [eax], dword 0xBD39D1D2 
	mov [eax+4], word 0xBA2F
	mov [eax+6], word 0x486a
	mov [eax+8], byte 0x89
	mov [eax+9], byte 0xB0
	mov [eax+10], byte 0xB4
	mov [eax+11], byte 0xB0
	mov [eax+12], byte 0xCB
	mov [eax+13], byte 0x46
	mov [eax+14], byte 0x68
	mov [eax+15], byte 0x91
	ret

_GetIID_ICorRuntimeHost:
	push 16
	call _malloc
	mov [eax], dword 0xcb2f6722 
	mov [eax+4], word 0xab3a
	mov [eax+6], word 0x11d2
	mov [eax+8], byte 0x9c
	mov [eax+9], byte 0x40
	mov [eax+10], byte 0x00
	mov [eax+11], byte 0xc0
	mov [eax+12], byte 0x4f
	mov [eax+13], byte 0xa3
	mov [eax+14], byte 0x0a
	mov [eax+15], byte 0x3e
	ret

_GetCLSID_CorRuntimeHost:
	push 16
	call _malloc
	mov [eax], dword 0xcb2f6723 
	mov [eax+4], word 0xab3a
	mov [eax+6], word 0x11d2
	mov [eax+8], byte 0x9c
	mov [eax+9], byte 0x40
	mov [eax+10], byte 0x00
	mov [eax+11], byte 0xc0
	mov [eax+12], byte 0x4f
	mov [eax+13], byte 0xa3
	mov [eax+14], byte 0x0a
	mov [eax+15], byte 0x3e
	ret

_GetIID_AppDomain:
	push 16
	call _malloc
	mov [eax], dword 0x05F696DC 
	mov [eax+4], word 0x2B29
	mov [eax+6], word 0x3663
	mov [eax+8], byte 0xAD
	mov [eax+9], byte 0x8B
	mov [eax+10], byte 0xC4
	mov [eax+11], byte 0x38
	mov [eax+12], byte 0x9C
	mov [eax+13], byte 0xF2
	mov [eax+14], byte 0xA7
	mov [eax+15], byte 0x13
	ret	

_GetNameSpaceAndClassName:
	;this would need to be manually changed based on if the classname changes etc
	;"TestLib.Class1"
	mov eax, dword [ebx+200]
	push eax
	call [ebx+340]
	ret




_GetFunctionName:
	;this would need to be manually changed based on if the classname changes etc
	;"Run"
	mov eax, dword [ebx+204]
	push eax
	call [ebx+340]
	ret

_CreateVariant:
	push 16
	call _malloc
	push eax
	call [ebx+348]
	ret