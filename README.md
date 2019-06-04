# 微信联系人净化器

注意：仅支持PC微信2.6.7.57，其他版本不支持！

使用C#/C++混合编程。

Purifier.exe：使用C#编写，主程序，完成基本的逻辑。
WxPurifier.dll：使用C++编写，注入微信中，用于拦截微信收到的消息，上报消息，调用微信中删除联系人的函数。
ComPurifier.dll：使用C#编写，发布为COM组件，封装了Http Web功能，用于WxPurifier.dll和Purifier.exe之间的通讯。

Purifier.exe启动后，检查创建配置信息，检查微信，向微信注入WxPurifier.dll，启动Web服务，开始接收数据.

WxPurifier.dll被注入到微信中后，启动微信消息拦截功能，同时加载ComPurifier.dll的COM组件。

在WxPurifier.dll截获到微信收到的消息后，如果是文本消息，则通过COM调用ComPurifier.dll中的方法将数据POST到管理端Purifier.exe。Purifier.exe检查数据，如果该信息中含有关键词（keywords），则通过Http post向ComPurifier.dll发送删除联系人的指令。ComPurifier.dll收到指令后，调用WxPurifier.dll中的函数，
WxPurifier.dll调用微信中删除联系人的函数，完成删除联系人的功能。






