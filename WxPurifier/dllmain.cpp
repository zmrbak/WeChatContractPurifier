// dllmain.cpp : 定义 DLL 应用程序的入口点。

#include "pch.h"
#include <Windows.h>
#include <string>
#include <strstream>

#import "ComPurifier.tlb"
#pragma comment(lib, "Version.lib")

using namespace  std;
using namespace ComPurifier;

VOID DoAction();
BOOL IsWxVersionValid(string);
extern "C" __declspec(dllexport) BOOL __stdcall DeleteWxUser(WCHAR* contractWxId);
VOID HookWx();
VOID RecieveMsgHook();
VOID RecieveMsg();
LPCWSTR GetMsgByAddress(DWORD memAddress);

//微信基址
HMODULE wxBaseAddress = 0;
//跳回地址
DWORD jumBackAddress = 0;
//我们要提取的寄存器内容
DWORD r_esp = 0;
//自己的wxid
wstring robot_wxid = L"";

//文本消息结构体
struct StructWxid
{
	//发送的文本消息指针
	wchar_t* pWxid;
	//字符串长度
	DWORD length;
	//字符串最大长度
	DWORD maxLength;

	//补充两个占位数据
	DWORD fill1 = 0;
	DWORD fill2 = 0;
};


BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	{
		//##########################################################
		//
		//注意：仅适配PC微信2.6.7.57版本，其它版本不可用
		//
		//##########################################################
		string wxVersoin = "2.6.7.57";
		if (IsWxVersionValid(wxVersoin) == FALSE)
		{
			string msg = "微信版本不匹配，程序不能继续执行！\n所需微信版本：";
			msg += wxVersoin;
			MessageBoxA(NULL, msg.c_str(), "错误", MB_OK | MB_ICONERROR);
		}
		else
		{
			HANDLE hANDLE = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)DoAction, NULL, NULL, 0);
			if (hANDLE != 0)
			{
				CloseHandle(hANDLE);
			}
		}
		break;
	}
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}
VOID DoAction()
{
	//Hook
	HookWx();

	//启动Web服务器
	HRESULT hRESULT = CoInitializeEx(NULL, NULL);
	ComPurifier::ICSharpPurifierPtr CSharpPurifierPtr(__uuidof(CSharpPurifier));
	VARIANT_BOOL result = CSharpPurifierPtr->StartWeb();
	if (result == 0)
	{
		MessageBoxA(NULL, "Web服务启动失败，请检查配置文件！", "错误", MB_OK | MB_ICONERROR);
	}

	while (true)
	{
		Sleep(1000);
	}

	CSharpPurifierPtr->Release();
	CoUninitialize();
}


BOOL __stdcall  DeleteWxUser(WCHAR* contractWxId)
{
	WCHAR wxid[50] = { 0 };
	DWORD callAdrress = (DWORD)wxBaseAddress + 0x26FB50;

	for (size_t i = 0; i < wcslen(contractWxId); i++)
	{
		wxid[i] = *(contractWxId + i);
	}

	//构建Wxid结构体
	StructWxid structWxid = { 0 };
	structWxid.pWxid = wxid;
	structWxid.length = wcslen(wxid);
	structWxid.maxLength = wcslen(wxid) * 2;

	//取wxid的地址
	DWORD* asmWxid = (DWORD*)& structWxid.pWxid;
	//BOOL returnValue = 0;

	//0FC5DBDD    51              push ecx
	//0FC5DBDE    57              push edi
	//0FC5DBDF    E8 6C1F1000     call WeChatWi.0FD5FB50                   ; 删除用户的ＣＡＬＬ
	//0FC5DBE4    8D8D 58FFFFFF   lea ecx,dword ptr ss:[ebp-0xA8]

	__asm
	{
		mov ecx, 0
		push ecx
		mov edi, asmWxid
		push edi
		call callAdrress
		//mov returnValue, eax
	}
	//return returnValue;
}

//检查微信版本是否匹配
BOOL IsWxVersionValid(string wxVersoin)
{
	wxBaseAddress = GetModuleHandleA("WeChatWin.dll");
	WCHAR VersionFilePath[MAX_PATH];
	if (GetModuleFileName(wxBaseAddress, VersionFilePath, MAX_PATH) == 0)
	{
		return FALSE;
	}

	string asVer = "";
	VS_FIXEDFILEINFO* pVsInfo;
	unsigned int iFileInfoSize = sizeof(VS_FIXEDFILEINFO);
	int iVerInfoSize = GetFileVersionInfoSize(VersionFilePath, NULL);
	if (iVerInfoSize != 0) {
		char* pBuf = new char[iVerInfoSize];
		if (GetFileVersionInfo(VersionFilePath, 0, iVerInfoSize, pBuf)) {
			if (VerQueryValue(pBuf, TEXT("\\"), (void**)& pVsInfo, &iFileInfoSize)) {
				//主版本2.6.7.57
				//2
				int s_major_ver = (pVsInfo->dwFileVersionMS >> 16) & 0x0000FFFF;
				//6
				int s_minor_ver = pVsInfo->dwFileVersionMS & 0x0000FFFF;
				//7
				int s_build_num = (pVsInfo->dwFileVersionLS >> 16) & 0x0000FFFF;
				//57
				int s_revision_num = pVsInfo->dwFileVersionLS & 0x0000FFFF;

				//把版本变成字符串
				strstream wxVer;
				wxVer << s_major_ver << "." << s_minor_ver << "." << s_build_num << "." << s_revision_num;
				wxVer >> asVer;
			}
		}
		delete[] pBuf;
	}
	//版本匹配
	if (asVer == wxVersoin)
	{
		return TRUE;
	}

	//版本不匹配
	return FALSE;
}


//Hook接收消息
VOID HookWx()
{
	//判断是否已经HOOK
	//WeChatWin.dll+0x310573
	DWORD hookAddress = (DWORD)wxBaseAddress + 0x310573;

	//跳回的地址
	jumBackAddress = hookAddress + 5;

	//组装跳转数据
	BYTE jmpCode[5] = { 0 };
	jmpCode[0] = 0xE9;

	//新跳转指令中的数据=跳转的地址-原地址（HOOK的地址）-跳转指令的长度
	*(DWORD*)& jmpCode[1] = (DWORD)RecieveMsgHook - hookAddress - 5;

	//覆盖指令
	//WeChatWin.dll + 310573 - B9 E8CF2B10 - mov ecx, WeChatWin.dll + 125CFE8{ (0) }
	WriteProcessMemory(GetCurrentProcess(), (LPVOID)hookAddress, jmpCode, 5, 0);
}

//跳转到这里，让我们自己处理消息
__declspec(naked) VOID RecieveMsgHook()
{
	//0FA20554  |.  C700 00000000 mov dword ptr ds:[eax],0x0
	//0FA2055A  |.  C740 04 00000>mov dword ptr ds:[eax+0x4],0x0
	//0FA20561  |.  C740 08 00000>mov dword ptr ds:[eax+0x8],0x0
	//0FA20568  |.  A3 1CCC9710   mov dword ptr ds:[0x1097CC1C],eax        ;  WeChatWi.10767140
	//0FA2056D  |>  50            push eax                                 ;  WeChatWi.10767140
	//0FA2056E  |.  A1 E8CF9610   mov eax,dword ptr ds:[0x1096CFE8]
	//HOOK这一句
	//0FA20573  |.  B9 E8CF9610   mov ecx,WeChatWi.1096CFE8


	//保存现场
	__asm
	{
		//补充被覆盖的代码
		//WeChatWin.dll + 310573 - B9 E8CF2B10 - mov ecx, WeChatWin.dll + 125CFE8{ (0) }
		//mov ecx,10CDCFE8
		mov ecx, 0x10CDCFE8

		//提取esp寄存器内容，放在一个变量中
		mov r_esp, esp

		//保存寄存器
		pushad
		pushf
	}

	//调用接收消息的函数
	RecieveMsg();

	//恢复现场
	__asm
	{
		popf
		popad

		//跳回被HOOK指令的下一条指令
		jmp jumBackAddress
	}
}

VOID RecieveMsg()
{
	wstring wxid = L"";
	wstring wxMsg=L"";
	
	//[[esp]]
	//信息块位置
	DWORD** msgAddress = (DWORD * *)r_esp;

	//排除自己发的消息
	DWORD isMyself = *((DWORD*)(**msgAddress + 0x34));
	if (isMyself == 1)
	{
		robot_wxid= GetMsgByAddress(**msgAddress + 0x40);
		return;
	}

	//消息类型[[esp]]+0x30
	//[01文字] [03图片] [31转账XML信息] [22语音消息] [02B视频信息]
	
	DWORD msgType = *((DWORD*)(**msgAddress + 0x30));
	if (msgType != 1)
	{
		return;
	}

	wxid = GetMsgByAddress(**msgAddress + 0x40);
	if (wxid.find(L"@") == wstring::npos)
	{
		wxMsg = GetMsgByAddress(**msgAddress + 0x68);
		
		wstring xml = L"<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
		xml.append(L"<WxTextMsg>");

		xml.append(L"<robot_wxid>");
		xml.append(L"<![CDATA[");
		xml.append(robot_wxid);
		xml.append(L"]]>");
		xml.append(L"</robot_wxid>");

		xml.append(L"<robot_nickname>");
		xml.append(L"<![CDATA[");
		xml.append(L".....");
		xml.append(L"]]>");
		xml.append(L"</robot_nickname>");

		xml.append(L"<from_wxid>");
		xml.append(L"<![CDATA[");
		xml.append(wxid);
		xml.append(L"]]>");
		xml.append(L"</from_wxid>");

		xml.append(L"<from_nickname>");
		xml.append(L"<![CDATA[");
		xml.append(L"......");
		xml.append(L"]]>");
		xml.append(L"</from_nickname>");

		xml.append(L"<msg>");
		xml.append(L"<![CDATA[");
		xml.append(wxMsg);
		xml.append(L"]]>");
		xml.append(L"</msg>");

		xml.append(L"</WxTextMsg>");

		HRESULT hRESULT = CoInitializeEx(NULL, NULL);
		ComPurifier::ICSharpPurifierPtr CSharpPurifierPtr(__uuidof(CSharpPurifier));
		CSharpPurifierPtr->PostString(xml.c_str());
		CSharpPurifierPtr->Release();
		CoUninitialize();
	}
}

//读取内存中的字符串
//存储格式
//xxxxxxxx:字符串地址（memAddress）
//xxxxxxxx:字符串长度（memAddress +4）
LPCWSTR GetMsgByAddress(DWORD memAddress)
{
	//获取字符串长度
	DWORD msgLength = *(DWORD*)(memAddress + 4);
	if (msgLength == 0)
	{
		WCHAR* msg = new WCHAR[1];
		msg[0] = 0;
		return msg;
	}

	WCHAR* msg = new WCHAR[msgLength + 1];
	ZeroMemory(msg, msgLength + 1);

	//复制内容
	wmemcpy_s(msg, msgLength + 1, (WCHAR*)(*(DWORD*)memAddress), msgLength + 1);
	return msg;
}
