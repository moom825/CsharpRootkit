_start: ; this is technically a function
	push rbp
    mov rbp, rsp
    sub rsp, 64             ; 8(return address)+0(varible data)+push(8)=16. aligned.
	mov r12, rcx
	
	;[r12] = HeapAlloc
	;[r12+8] = HeapFree
	;[r12+16]=GetProcessHeap
	;[r12+24]=LoadLibraryA
	;[r12+32]=GetProcAddress
	;[r12+40]=fileaddr
	;[r12+48]=filelength
	
	call [r12+16]
	mov rcx, rax
	mov rdx, 0x00000008
	mov r8, 1024
	call [r12]
	mov rbx, rax;rbx is the global varible storage

	mov rax, [r12]
	mov qword [rbx+400], rax
	mov rax, [r12+8]
	mov qword [rbx+408], rax
	mov rax, [r12+16]
	mov qword [rbx+416], rax
	mov rax, [r12+24]
	mov qword [rbx+424], rax
	mov rax, [r12+32]
	mov qword [rbx+432], rax	
	mov rax, [r12+40]
	mov qword [rbx+208], rax
	mov rax, [r12+48]
	mov qword [rbx+216], rax
	;[rbx+208] is the pointer to the file bytes
	;[rbx+216] is the file bytes length

	;[rbx+400] is the function HeapAlloc
	;[rbx+408] is the function HeapFree
	;[rbx+416] is the function GetProcessHeap
	;[rbx+424] is the function LoadLibraryA
	;[rbx+432] is the function GetProcAddress

	mov rax, r12
	add rax, 64
	mov qword [rbx+600], rax

	mov rax, r12
	add rax, 64
	add rax, qword [r12+56];length of the NamespaceAndClassString
	add rax, 8;skip over the length of the next string
	mov qword [rbx+608], rax

	call _init
	cmp rax, 0
	je .ErrorOccured
	;[rbx+400] is the function HeapAlloc
	;[rbx+408] is the function HeapFree
	;[rbx+416] is the function GetProcessHeap
	;[rbx+424] is the function LoadLibraryA
	;[rbx+432] is the function GetProcAddress
	;[rbx+440] is the pointer to mscoree.dll
	;[rbx+448] is the pointer to oleaut32.dll
	;[rbx+456] is the function SafeArrayCreate
	;[rbx+464] is the function SafeArrayLock
	;[rbx+472] is the function SafeArrayUnlock
	;[rbx+480] is the function SysAllocString
	;[rbx+488] is the function SafeArrayCreateVector
	;[rbx+496] is the function VariantInit
	;[rbx+504] is the function CLRCreateInstance

	mov qword [rbx+8], rbx
	call _GetCLSID_CLRMetaHost
	mov qword [rbx+16], rax
	call _GetIID_ICLRMetaHost
	mov qword [rbx+24], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	
	mov rcx, qword [rbx+16]
	mov rdx, qword [rbx+24]
	mov r8, qword [rbx+8]
	call [rbx+504]
    cmp rax, 0
	jne .ErrorOccured
	;TODO: make sure rax is 0, do something on err
	
	mov rax, qword [rbx+0]
	mov rax, qword [rax]
	mov rax, [rax+40]
	mov qword [rbx+32], rax
	mov rax, rbx
	add rax, 40
	mov qword [rbx+48], rax
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	
	mov rcx, qword [rbx+0]
	mov rdx, qword [rbx+48]
	call [rbx+32]
	cmp rax, qword 0
	jne .ErrorOccured
	;TODO: make sure rax is 0, do something on err
	
	call _GetIID_ICLRRuntimeInfo
	mov qword [rbx+56], rax
	
	mov rax, rbx
	add rax, 64
	mov qword [rbx+72], rax
	mov rax, rbx
	add rax, 80
	mov qword [rbx+88], rax
	mov rax, qword [rbx+40]
	mov rax, qword [rax]
	mov rax, qword [rax+24]
	mov qword [rbx+96], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	
	.getLastestRuntimeLoop:
		mov rcx, [rbx+40]
		mov rdx, dword 1
		mov r8, [rbx+88]
		mov r9, dword 0
		call [rbx+96]
		cmp rax, 0
		jne .getLastestRuntimeLoopDone
		
		mov rax, qword [rbx+80]
		mov rax, qword [rax]
		mov rax, qword [rax+0]
		mov rcx, [rbx+80]
		mov rdx, [rbx+56]
		mov r8, [rbx+72]
		call rax
		jmp .getLastestRuntimeLoop
	
	.getLastestRuntimeLoopDone:
	cmp qword [rbx+64], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov rax, qword [rbx+64]
	mov rax, qword [rax]
	mov rax, qword [rax+72]
	mov qword [rbx+104], rax
	
	call _GetIID_ICorRuntimeHost
	mov qword [rbx+112], rax
	call _GetCLSID_CorRuntimeHost
	mov qword [rbx+120], rax
	
	mov rax, rbx
	add rax, 128
	mov [rbx+136], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	
	
	mov rcx, qword [rbx+64]
	mov rdx, qword [rbx+120]
	mov r8, qword [rbx+112]
	mov r9, qword [rbx+136]
	call qword [rbx+104]
	cmp rax, 0
	jne .ErrorOccured
	;TODO: make sure rax is 0, do something on err
	
	cmp qword [rbx+128], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov rax, qword [rbx+128]
	mov rax, qword [rax]
	mov rax, qword [rax+80]
	mov qword [rbx+144], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	mov rcx, qword [rbx+128]
	call [rbx+144]
	cmp rax, 0
	jne .ErrorOccured
	;TODO: make sure its 0, do something on err
	
	
	mov rax, qword [rbx+128]
	mov rax, qword [rax]
	mov rax, qword [rax+104]
	mov qword [rbx+152], rax
	
	mov rax, rbx
	add rax, 160
	mov qword [rbx+168], rax
	
	call _GetIID_AppDomain
	mov qword [rbx+176], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	;[rbx+152] is the pointer to the GetDefaultDomain function
	;[rbx+160] is the pointer to appDomainThunk, aka *appDomainThunk
	;[rbx+168] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[rbx+176] is the potiner to IID_AppDomain
	
	mov rcx, qword [rbx+128]
	mov rdx, qword [rbx+168]
	call [rbx+152]
	
	cmp qword [rbx+160], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov rax, rbx
	add rax, 184
	mov qword [rbx+192], rax
	
	mov rax, qword [rbx+160]
	mov rax, qword [rax]
	mov rax, qword [rax+0]
	mov qword [rbx+200], rax
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	;[rbx+152] is the pointer to the GetDefaultDomain function
	;[rbx+160] is the pointer to appDomainThunk, aka *appDomainThunk
	;[rbx+168] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[rbx+176] is the potiner to IID_AppDomain
	;[rbx+184] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[rbx+192] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[rbx+200] is the function QueryInterface
	
	
	mov rcx, qword [rbx+160]
	mov rdx, qword [rbx+176]
	mov r8,  qword [rbx+192]
	call [rbx+200]
	
	cmp qword [rbx+184], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err

	mov rcx, 8
	call _malloc
	mov [rbx+224], rax
	
	mov rdx, qword [rbx+216]
	mov dword [rax], edx;lower 32 bits of rdx
	mov dword [rax+4], 0
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	;[rbx+152] is the pointer to the GetDefaultDomain function
	;[rbx+160] is the pointer to appDomainThunk, aka *appDomainThunk
	;[rbx+168] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[rbx+176] is the potiner to IID_AppDomain
	;[rbx+184] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[rbx+192] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[rbx+200] is the function QueryInterface
	;[rbx+208] is the pointer to the file bytes
	;[rbx+216] is the file bytes length
	;[rbx+224] is the pointer to bounds, aka *bounds
	
	
	
	mov rcx, 17
	mov rdx, 1
	mov r8, qword [rbx+224]
	call [rbx+456]
	mov qword [rbx+232], rax
	
	mov rcx, qword [rbx+232]
	call [rbx+464]
	
	mov rax, [rbx+232]
	
	mov rcx, qword [rax+16];pvdata
	mov rdx, qword [rbx+208]
	mov r8, qword [rbx+216]
	call _memcopy
	
	mov rcx, qword [rbx+232]
	call [rbx+472]
	
	mov rax, rbx
	add rax, 240
	mov qword [rbx+248], rax
	
	mov rax, qword [rbx+184]
	mov rax, qword [rax]
	mov rax, qword [rax+360]
	mov qword [rbx+256], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	;[rbx+152] is the pointer to the GetDefaultDomain function
	;[rbx+160] is the pointer to appDomainThunk, aka *appDomainThunk
	;[rbx+168] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[rbx+176] is the potiner to IID_AppDomain
	;[rbx+184] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[rbx+192] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[rbx+200] is the function QueryInterface
	;[rbx+208] is the pointer to the file bytes
	;[rbx+216] is the file bytes length
	;[rbx+224] is the pointer to bounds, aka *bounds
	;[rbx+232] is the potiner to safeArray, aka *safeArray
	;[rbx+240] is the pointer to managedAssembly, aka *managedAssembly
	;[rbx+248] is the pointer to a pointer of managedAssembly, aka **managedAssembly
	;[rbx+256] is the pointer to the function Load_3
	
	
	mov rcx, qword [rbx+184]
	mov rdx, qword [rbx+232]
	mov r8,  qword [rbx+248]
	call [rbx+256]
	cmp rax, 0
	
	cmp qword [rbx+240], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov rax, rbx
	add rax, 264
	mov qword [rbx+272], rax
	
	call _GetNameSpaceAndClassName
	mov qword [rbx+280], rax
	
	mov rax, qword [rbx+240]
	mov rax, qword [rax]
	mov rax, qword [rax+136]
	mov qword [rbx+288], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	;[rbx+152] is the pointer to the GetDefaultDomain function
	;[rbx+160] is the pointer to appDomainThunk, aka *appDomainThunk
	;[rbx+168] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[rbx+176] is the potiner to IID_AppDomain
	;[rbx+184] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[rbx+192] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[rbx+200] is the function QueryInterface
	;[rbx+208] is the pointer to the file bytes
	;[rbx+216] is the file bytes length
	;[rbx+224] is the pointer to bounds, aka *bounds
	;[rbx+232] is the potiner to safeArray, aka *safeArray
	;[rbx+240] is the pointer to managedAssembly, aka *managedAssembly
	;[rbx+248] is the pointer to a pointer of managedAssembly, aka **managedAssembly
	;[rbx+256] is the pointer to the function Load_3
	;[rbx+264] is the potiner to managedType, aka *managedType
	;[rbx+272] is the potiner to a pointer of managedType, aka **managedType
	;[rbx+280] is the pointer to the NamespaceAndClassString
	;[rbx+288] is the pointer to the function GetType_2
	
	mov rcx, qword [rbx+240]
	mov rdx, qword [rbx+280]
	mov r8, qword [rbx+272]
	call [rbx+288]
	
	cmp rax, 0
	jne .ErrorOccured
	;TODO: make sure rax is 0, do something on err
	
	cmp qword [rbx+264], 0
	je .ErrorOccured
	;TODO: make sure its not 0, do something on err
	
	mov rcx, 12; VT_VARIANT
	mov rdx, 0
	mov r8, 0;zero arguments
	call [rbx+488]
	mov qword [rbx+296], rax
	
	call _CreateVariant
	mov qword [rbx+304], rax

	call _CreateVariant
	mov qword [rbx+312], rax
	
	mov rax, qword [rbx+264]
	mov rax, qword [rax]
	mov rax, qword [rax+456]
	mov qword [rbx+320], rax
	
	call _GetFunctionName
	mov qword [rbx+328], rax
	
	;[rbx+0] is the pointer to metaHost aka *metahost
	;[rbx+8] is the pointer to a pointer of metaHost aka **metahost
	;[rbx+16] is the pointer to CLSID_CLRMetaHost
	;[rbx+24] is the pointer to IID_ICLRMetaHost
	;[rbx+32] holds EnumerateInstalledRuntimes function
	;[rbx+40] holds the pointer to runtime, aka *runtime
	;[rbx+48] holds the pointer to a pointer of runtime, aka **runtime
	;[rbx+56] is the pointer to IID_ICLRRuntimeInfo
	;[rbx+64] is the pointer to runtimeinfo, aka *runtimeinfo
	;[rbx+72] is the pointer to the pointer of runtimeinfo, aks **runtimeinfo
	;[rbx+80] is the pointer to enumRuntime, aka *enumRuntime
	;[rbx+88] is the pointer to the pointer of enumRuntime, aka **enumRuntime
	;[rbx+96] is the pointer to the function Next
	;[rbx+104] is the pointer to the function GetInterface
	;[rbx+112] is the pointer to IID_ICorRuntimeHost
	;[rbx+120] is the pointer to CLSID_CorRuntimeHost
	;[rbx+128] is the pointer to runtimeHost, aka *runtimeHost
	;[rbx+136] is the pointer to a pointer of runtimeHost, aka **runtimeHost
	;[rbx+144] is the pointer to the start function
	;[rbx+152] is the pointer to the GetDefaultDomain function
	;[rbx+160] is the pointer to appDomainThunk, aka *appDomainThunk
	;[rbx+168] is the pointer to a pointer of appDomainThunk, aka **appDomainThunk
	;[rbx+176] is the potiner to IID_AppDomain
	;[rbx+184] is the pointer to defaultAppDomain, aka *defaultAppDomain
	;[rbx+192] is the pointer to a potiner of defaultAppDomain, aka **defaultAppDomain
	;[rbx+200] is the function QueryInterface
	;[rbx+208] is the pointer to the file bytes
	;[rbx+216] is the file bytes length
	;[rbx+224] is the pointer to bounds, aka *bounds
	;[rbx+232] is the potiner to safeArray, aka *safeArray
	;[rbx+240] is the pointer to managedAssembly, aka *managedAssembly
	;[rbx+248] is the pointer to a pointer of managedAssembly, aka **managedAssembly
	;[rbx+256] is the pointer to the function Load_3
	;[rbx+264] is the potiner to managedType, aka *managedType
	;[rbx+272] is the potiner to a pointer of managedType, aka **managedType
	;[rbx+280] is the pointer to the NamespaceAndClassString
	;[rbx+288] is the pointer to the function GetType_2	
	;[rbx+296] is the pointer to managedArguments, aka *managedArguments
	;[rbx+304] is the potiner to managedReturnValue, aka *managedReturnValue
	;[rbx+312] is the potiner to empty, aka *empty
	;[rbx+320] is the pointer to the function InvokeMember_3
	;[rbx+328] is the pointer to the MethodName
	
	
	mov rcx, qword [rbx+264]
    mov rdx, qword [rbx+328]
    mov r8, qword 280
    mov r9, 0 
	mov rax, qword [rbx+304] 
    mov [rsp+32], rax
    mov rax, qword [rbx+296] 
    mov [rsp+40], rax
	
	mov rsi, qword [rbx+312]
	
	mov rax, qword [rsi]
    mov [rsp+48], rax
    mov rax, qword [rsi+8]
    mov [rsp+56], rax
    mov rax, qword [rsi+16]
    mov [rsp+62], rax
	
    call qword [rbx+320]
	cmp rax, 0
	jne .ErrorOccured 
	
	mov rsp, rbp
    pop rbp
	ret

	.ErrorOccured:
	mov rax, 0xffffffffffffffff
	mov rsp, rbp
    pop rbp
	ret
_malloc:
	push rbp               ; Save the base pointer of the caller
    mov rbp, rsp           ; Set the base pointer for the current function
    sub rsp, 32            ; 8(return address)+32(varible data)+push(8)=48. 16 byte aligned.
	mov qword [rbp-8], rcx
	call [rbx+416]
	mov rcx, rax         
    mov rdx, 0x00000008  
    mov r8, qword [rbp-8]
	call [rbx+400]
	mov rsp, rbp
    pop rbp
    ret

_mallocFree:
	push rbp               ; Save the base pointer of the caller
    mov rbp, rsp           ; Set the base pointer for the current function
    sub rsp, 32            ; 8(return address)+32(varible data)+push(8)=48. 16 byte aligned.
	mov qword [rbp-8], rcx
	call [rbx+416]
	mov rcx, rax         
    mov rdx, 0  
    mov r8, qword [rbp-8]
	call [rbx+408]
	mov rsp, rbp
    pop rbp
    ret

_GetCLSID_CLRMetaHost:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 16            ; Size to allocate
    call _malloc
	mov [rax], dword 0x9280188d 
	mov [rax+4], word 0xe8e
	mov [rax+6], word 0x4867
	mov [rax+8], byte 0xb3
	mov [rax+9], byte 0xc
	mov [rax+10], byte 0x7f
	mov [rax+11], byte 0xa8
	mov [rax+12], byte 0x38
	mov [rax+13], byte 0x84
	mov [rax+14], byte 0xe8
	mov [rax+15], byte 0xde
    mov rsp, rbp
    pop rbp
    ret

_GetIID_ICLRMetaHost:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 16            ; Size to allocate
    call _malloc
	mov [rax], dword 0xD332DB9E 
	mov [rax+4], word 0xB9B3
	mov [rax+6], word 0x4125
	mov [rax+8], byte 0x82
	mov [rax+9], byte 0x07
	mov [rax+10], byte 0xA1
	mov [rax+11], byte 0x48
	mov [rax+12], byte 0x84
	mov [rax+13], byte 0xF5
	mov [rax+14], byte 0x32
	mov [rax+15], byte 0x16
    mov rsp, rbp
    pop rbp
    ret

_GetIID_ICLRRuntimeInfo:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 16            ; Size to allocate
    call _malloc
	mov [rax], dword 0xBD39D1D2 
	mov [rax+4], word 0xBA2F
	mov [rax+6], word 0x486a
	mov [rax+8], byte 0x89
	mov [rax+9], byte 0xB0
	mov [rax+10], byte 0xB4
	mov [rax+11], byte 0xB0
	mov [rax+12], byte 0xCB
	mov [rax+13], byte 0x46
	mov [rax+14], byte 0x68
	mov [rax+15], byte 0x91
    mov rsp, rbp
    pop rbp
    ret

_GetIID_ICorRuntimeHost:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 16            ; Size to allocate
    call _malloc
	mov [rax], dword 0xcb2f6722 
	mov [rax+4], word 0xab3a
	mov [rax+6], word 0x11d2
	mov [rax+8], byte 0x9c
	mov [rax+9], byte 0x40
	mov [rax+10], byte 0x00
	mov [rax+11], byte 0xc0
	mov [rax+12], byte 0x4f
	mov [rax+13], byte 0xa3
	mov [rax+14], byte 0x0a
	mov [rax+15], byte 0x3e
    mov rsp, rbp
    pop rbp
    ret

_GetCLSID_CorRuntimeHost:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 16            ; Size to allocate
    call _malloc
	mov [rax], dword 0xcb2f6723 
	mov [rax+4], word 0xab3a
	mov [rax+6], word 0x11d2
	mov [rax+8], byte 0x9c
	mov [rax+9], byte 0x40
	mov [rax+10], byte 0x00
	mov [rax+11], byte 0xc0
	mov [rax+12], byte 0x4f
	mov [rax+13], byte 0xa3
	mov [rax+14], byte 0x0a
	mov [rax+15], byte 0x3e
    mov rsp, rbp
    pop rbp
    ret

_GetIID_AppDomain:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 16            ; Size to allocate
    call _malloc
	mov [rax], dword 0x05F696DC 
	mov [rax+4], word 0x2B29
	mov [rax+6], word 0x3663
	mov [rax+8], byte 0xAD
	mov [rax+9], byte 0x8B
	mov [rax+10], byte 0xC4
	mov [rax+11], byte 0x38
	mov [rax+12], byte 0x9C
	mov [rax+13], byte 0xF2
	mov [rax+14], byte 0xA7
	mov [rax+15], byte 0x13
    mov rsp, rbp
    pop rbp
    ret	

_memcopy:
	push rbp
    mov rbp, rsp
    sub rsp, 32
	;mov rcx, target
	;mov rdx, source
	;mov r8, count
	
	.copy_loop:
		mov al, byte [rdx]
		mov [rcx], al
		inc rdx
		inc rcx
		dec r8
		cmp r8, 0
		jne .copy_loop
    mov rax, 1
	mov rsp, rbp
    pop rbp
    ret	




_GetFunctionName:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.

	mov rax, qword [rbx+608]
	mov rcx, rax
	call [rbx+480]
    mov rsp, rbp
    pop rbp
    ret


_GetNameSpaceAndClassName:
    push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
	
	mov rax, qword [rbx+600]
	mov rcx, rax
	call [rbx+480]
    
	mov rsp, rbp
    pop rbp
    ret

_CreateVariant:
	push rbp
    mov rbp, rsp
    sub rsp, 32             ; 8(return address)+32(varible data)+push(8)=48. aligned.
    mov rcx, 24            ; Size to allocate
    call _malloc
	push rax
	mov rcx, rax
	call [rbx+496]
	pop rax
    mov rsp, rbp
    pop rbp
    ret

_init:;this is to load all the needed libraries
	;[rbx+400] is the function HeapAlloc
	;[rbx+408] is the function HeapFree
	;[rbx+416] is the function GetProcessHeap
	;[rbx+424] is the function LoadLibraryA
	;[rbx+432] is the function GetProcAddress
	push rbp
    mov rbp, rsp
    sub rsp, 40             ; 8(return address)+32(varible data)+8(offset)+push(8)+push(8) =64. aligned.	
    push r12
	mov rcx, 12
	call _malloc
	mov byte [rax], 'm'
	mov byte [rax+1], 's'
	mov byte [rax+2], 'c'
	mov byte [rax+3], 'o'
	mov byte [rax+4], 'r'
	mov byte [rax+5], 'e'
	mov byte [rax+6], 'e'
	mov byte [rax+7], '.'
	mov byte [rax+8], 'd'
	mov byte [rax+9], 'l'
	mov byte [rax+10], 'l'
	mov byte [rax+11], 0
	mov r12, rax
	mov rcx, rax
	call [rbx+424]
	cmp rax, 0
	je .InitErrorOccured
	mov qword [rbx+440], rax
	mov rcx, r12
	call _mallocFree
	
	mov rcx, 13
	call _malloc
	mov byte [rax], 'o'
	mov byte [rax+1], 'l'
	mov byte [rax+2], 'e'
	mov byte [rax+3], 'a'
	mov byte [rax+4], 'u'
	mov byte [rax+5], 't'
	mov byte [rax+6], '3'
	mov byte [rax+7], '2'
	mov byte [rax+8], '.'
	mov byte [rax+9], 'd'
	mov byte [rax+10], 'l'
	mov byte [rax+11], 'l'
	mov byte [rax+12], 0
	mov r12, rax
	mov rcx, rax
	call [rbx+424]
	cmp rax, 0
	je .InitErrorOccured
	mov qword [rbx+448], rax
	mov rcx, r12
	call _mallocFree
	
	;[rbx+400] is the function HeapAlloc
	;[rbx+408] is the function HeapFree
	;[rbx+416] is the function GetProcessHeap
	;[rbx+424] is the function LoadLibraryA
	;[rbx+432] is the function GetProcAddress
	;[rbx+440] is the pointer to mscoree.dll
	;[rbx+448] is the pointer to oleaut32.dll
	
	mov rcx, 16;oleaut32.dll
	call _malloc
	mov byte [rax+0], 'S'
	mov byte [rax+1], 'a'
	mov byte [rax+2], 'f'
	mov byte [rax+3], 'e'
	mov byte [rax+4], 'A'
	mov byte [rax+5], 'r'
	mov byte [rax+6], 'r'
	mov byte [rax+7], 'a'
	mov byte [rax+8], 'y'
	mov byte [rax+9], 'C'
	mov byte [rax+10], 'r'
	mov byte [rax+11], 'e'
	mov byte [rax+12], 'a'
	mov byte [rax+13], 't'
	mov byte [rax+14], 'e'
	mov byte [rax+15], 0
	mov r12, rax
	mov rcx, qword [rbx+448]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+456], rax
	mov rcx, r12
	call _mallocFree
	
	mov rcx, 14;oleaut32.dll
	call _malloc
	mov byte [rax+0], 'S'
	mov byte [rax+1], 'a'
	mov byte [rax+2], 'f'
	mov byte [rax+3], 'e'
	mov byte [rax+4], 'A'
	mov byte [rax+5], 'r'
	mov byte [rax+6], 'r'
	mov byte [rax+7], 'a'
	mov byte [rax+8], 'y'
	mov byte [rax+9], 'L'
	mov byte [rax+10], 'o'
	mov byte [rax+11], 'c'
	mov byte [rax+12], 'k'
	mov byte [rax+13], 0
	mov r12, rax
	mov rcx, qword [rbx+448]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+464], rax
	mov rcx, r12
	call _mallocFree
	mov rcx, 16;oleaut32.dll
	call _malloc
	mov byte [rax+0], 'S'
	mov byte [rax+1], 'a'
	mov byte [rax+2], 'f'
	mov byte [rax+3], 'e'
	mov byte [rax+4], 'A'
	mov byte [rax+5], 'r'
	mov byte [rax+6], 'r'
	mov byte [rax+7], 'a'
	mov byte [rax+8], 'y'
	mov byte [rax+9], 'U'
	mov byte [rax+10], 'n'
	mov byte [rax+11], 'l'
	mov byte [rax+12], 'o'
	mov byte [rax+13], 'c'
	mov byte [rax+14], 'k'
	mov byte [rax+15], 0
	mov r12, rax
	mov rcx, qword [rbx+448]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+472], rax
	mov rcx, r12
	call _mallocFree
	mov rcx, 15;oleaut32.dll
	call _malloc
	mov byte [rax+0], 'S'
	mov byte [rax+1], 'y'
	mov byte [rax+2], 's'
	mov byte [rax+3], 'A'
	mov byte [rax+4], 'l'
	mov byte [rax+5], 'l'
	mov byte [rax+6], 'o'
	mov byte [rax+7], 'c'
	mov byte [rax+8], 'S'
	mov byte [rax+9], 't'
	mov byte [rax+10], 'r'
	mov byte [rax+11], 'i'
	mov byte [rax+12], 'n'
	mov byte [rax+13], 'g'
	mov byte [rax+14], 0
	mov r12, rax
	mov rcx, qword [rbx+448]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+480], rax
	mov rcx, r12
	call _mallocFree
	mov rcx, 22;oleaut32.dll
	call _malloc
	mov byte [rax+0], 'S'
	mov byte [rax+1], 'a'
	mov byte [rax+2], 'f'
	mov byte [rax+3], 'e'
	mov byte [rax+4], 'A'
	mov byte [rax+5], 'r'
	mov byte [rax+6], 'r'
	mov byte [rax+7], 'a'
	mov byte [rax+8], 'y'
	mov byte [rax+9], 'C'
	mov byte [rax+10], 'r'
	mov byte [rax+11], 'e'
	mov byte [rax+12], 'a'
	mov byte [rax+13], 't'
	mov byte [rax+14], 'e'
	mov byte [rax+15], 'V'
	mov byte [rax+16], 'e'
	mov byte [rax+17], 'c'
	mov byte [rax+18], 't'
	mov byte [rax+19], 'o'
	mov byte [rax+20], 'r'
	mov byte [rax+21], 0
	mov r12, rax
	mov rcx, qword [rbx+448]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+488], rax
	mov rcx, r12
	call _mallocFree
	mov rcx, 12;oleaut32.dll
	call _malloc
	mov byte [rax+0], 'V'
	mov byte [rax+1], 'a'
	mov byte [rax+2], 'r'
	mov byte [rax+3], 'i'
	mov byte [rax+4], 'a'
	mov byte [rax+5], 'n'
	mov byte [rax+6], 't'
	mov byte [rax+7], 'I'
	mov byte [rax+8], 'n'
	mov byte [rax+9], 'i'
	mov byte [rax+10], 't'
	mov byte [rax+11], 0
	mov r12, rax
	mov rcx, qword [rbx+448]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+496], rax
	mov rcx, r12
	call _mallocFree
	mov rcx, 18
	call _malloc
	mov byte [rax+0], 'C'
	mov byte [rax+1], 'L'
	mov byte [rax+2], 'R'
	mov byte [rax+3], 'C'
	mov byte [rax+4], 'r'
	mov byte [rax+5], 'e'
	mov byte [rax+6], 'a'
	mov byte [rax+7], 't'
	mov byte [rax+8], 'e'
	mov byte [rax+9], 'I'
	mov byte [rax+10], 'n'
	mov byte [rax+11], 's'
	mov byte [rax+12], 't'
	mov byte [rax+13], 'a'
	mov byte [rax+14], 'n'
	mov byte [rax+15], 'c'
	mov byte [rax+16], 'e'
	mov byte [rax+17], 0
	mov r12, rax
	mov rcx, qword [rbx+440]
	mov rdx, rax
	call [rbx+432]
	cmp rax, 0
	je .InitErrorOccured
	mov [rbx+504], rax
	mov rcx, r12
	call _mallocFree
	;[rbx+400] is the function HeapAlloc
	;[rbx+408] is the function HeapFree
	;[rbx+416] is the function GetProcessHeap
	;[rbx+424] is the function LoadLibraryA
	;[rbx+432] is the function GetProcAddress
	;[rbx+440] is the pointer to mscoree.dll
	;[rbx+448] is the pointer to oleaut32.dll
	;[rbx+456] is the function SafeArrayCreate
	;[rbx+464] is the function SafeArrayLock
	;[rbx+472] is the function SafeArrayUnlock
	;[rbx+480] is the function SysAllocString
	;[rbx+488] is the function SafeArrayCreateVector
	;[rbx+496] is the function VariantInit
	;[rbx+504] is the function CLRCreateInstance
	mov rax, 1
	pop r12
	mov rsp, rbp
    pop rbp
    ret
	.InitErrorOccured:
	mov rax, 0
	pop r12
	mov rsp, rbp
    pop rbp
    ret