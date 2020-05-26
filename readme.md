# USB new DAQ

> 这个项目本身的目的是为了开发一个新的上位机软件，取代原来那个老的软件，逐渐加入了新的功能，现在把固件和FPGA代码一起加上



# 安装驱动

在 CyPress 官网的 [FX3 界面](https://www.cypress.com/documentation/software-and-drivers/ez-usb-fx3-software-development-kit)下载对应的安装文件，安装好之后，更新设备驱动

如果已经加载了固件可以看到这个![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/FX3_stream_device.png)

如果没有加载固件这个设备是一个 Boot Loader，也就是可以烧写固件



# 软件

## 开发说明

代码说明见[使用生产者消费者模式获取 CyPress 数据](https://www.yuque.com/wyu0725/my_lab_skill/producer_consumer_pattern_in_cyusb)

## 使用说明

### 连接设备

首先软件打开是这个样子的

![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/usb_new_nodevice.png)



然后先选择USB设备，之后点击set，In 和 out EndPoint是只有一个选项，因为这个固件是这个设计的，至于Xfer package 和 Xfer queue 对于我的固件Xfer package 不要超过 256，因为我的一个上传的 package 是这么大，其大了之后会影响速度，Xfer queue 设置1其实就够快了，超过 16 意义不大，多线程也没必要多这么多；如果要改的话看看代码，看得懂可以自己修改，不行的话还是用默认吧。

   ![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/usb_new_daq_set_device.png)



要是不幸插到了 USB2.0 接口上也行，反正速度也就这样了，突然发现电脑上有个 3.0 接口是假的，插上之后是这反应。

![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/USB_new_daq_2.png)



### 发送数据

数据发送有两个选项，一个是手动敲，另外一个就是直接从文件读取，默认发的是16进制的文本，加不加0x都行；大端小端根据自己的项目来决定，我的逻辑是小端。

### 接受数据

就直接点start就好了。

注意，图中的 Failure并不是说传输失败了，而是一次 ACQ 周期没有获得数据 ([ACQ循环参见我的博客]((https://www.yuque.com/wyu0725/my_lab_skill/producer_consumer_pattern_in_cyusb)))

Pkg Error 是针对我用计数器实验来说的，也就是 [上一个包的包尾] = [下一个包的包头] - 1，否则的话肯定是丢数了，实验证明这个速度下不会丢数，查看硬盘写入速度还可以，反正没事儿别瞎玩儿，IO 压力这么大，也不知道硬盘会不会崩，再加上现在都是叠瓦式硬盘，越玩越慢，要是可以有固态来玩玩这个就不错。

![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/usb_new_daq_acq.png)

![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/usb_new_daq_hdd.png)



# FPGA代码

## 生成工程

直接在 Vivado 中运行运行 [gbt_usb3_create.tcl](\scripts\gbt_usb3_create.tcl)，然后编译即可，warning 都不带报一个！

![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/usb_new_daq_nowarning.png)

当然 ./src/ 路径下还有 2.0 的代码，推荐后续 USB2.0 的版本也改成这个，之前的代码用起来实在不科学！



# 注意事项

在采数的时候别关电源！！！不然程序会找不到设备描述符然后就崩溃了，目前没有想到办法避免这个问题，崩溃了就重开吧。



------

按惯例后记，代码工程随便用，反正也是在官网基础上改的，有啥 bug 我遇到的都写了，出现问题自己先查查尝试解决，不行再说。

欢迎关注[我的博客](https://wyu0725.github.io/)、[语雀](https://www.yuque.com/wyu0725)和公众号

![](https://raw.githubusercontent.com/wyu0725/My_Pic_bed/master/img/MyWXQRCode.png)