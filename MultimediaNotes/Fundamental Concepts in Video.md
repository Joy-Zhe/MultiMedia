# 1. Types of Video Signals 

## 1.1 Coponent Video

+ 可用于传输三个通道颜色分量的数据，三个通道可以互不干扰，因为有三根线![[0308_1.png]]

## 1.2 Composite Video

+ 先将图像转化为YUV/YIQ，只有一根线，但是干扰和带宽问题会出现，好处是兼容性比较好，同时支持黑白和彩色电视机![[0308_2.png]]

## 1.3 S-Video

+ 是一种妥协，Y信号独立传输，因为人眼对于明暗度比较敏感![[0308_3.png]]


# 2. Analog Video

## Concepts

+ Analog signal: f(t)-- time-varying images
+ 逐行扫描
+ 隔行扫描
+ 电视机一般使用隔行扫描，先扫奇数行，再扫偶数行，那么一张图片可以被分成两部分
+ 回溯时间：扫描完一行后，电子枪需要关闭一段时间，移动到下一个需要扫描的行的起点再开始扫描，这段时间成为回溯时间![[0308_4.png]]
+ 水平回溯时间：准备下一行，水平移动至下一行起点即可
+ 垂直回溯时间：偶数行扫完了，要扫奇数行，需要垂直移动
+ 故电子枪会有这样的电压曲线：![[0308_5.png]]
+ 视频制式：视频在采集时就不同，NTSC PAL SECAM![[0308_6.png]]
+ 制式和硬件相关，格式和硬件无关
## 2.1 NTSC

+ ![[0308_8.png]]
+ 每秒29.97帧，因为每一帧是33.37ms(1s/33.37ms)
+ 因为是隔行扫描，每帧的奇偶两个场分别有525/2=262.5行
+ 总共需要扫描$525\times 29.97=15734$行
+ 垂直回溯和水平回溯需要占用时间，垂直回溯预留了20行给每个field(每一帧需要两次垂直回溯，一次用于奇偶场间移动电子枪，一次用于移动到新一帧的起始位置)；水平回溯大约占每行时间的1/6。这些时间导致NTSC制式的视频会存在消隐行以及垂直消隐，即实际上的可视图像范围会比原先的分辨率略小(如下图，**白色部分不可见**，**灰色部分是实际可见部分**)：![[0308_9.png]]
### 信号调制

> 如何进行调制

![[0308_10.png]]
+ YIQ的信号分布：Y(低频)，IQ(高频)![[0308_11.png]]
> 如何进行解调
1. 先用低通滤波器获取Y通道(低频信号) $$composite = Y+C=Y+Icos(F_{sc}t)+Qsin(F_{sc}t)$$
2. 从高频信号中解出I和Q分量：$$C\times 2cos(F_{sc}t)=I+Icos(2F_{sc}t)+Q2sin(2F_{sc}t)$$
3. 再应用低通滤波器取出I分量

## 2.2 PAL

+ 625 scan lines
+ 25 frames/second 
+ 4:3 aspect ratio
+ 25 fps or 40 ms per frame
+ Interlace scan——312.5lines/fields
+ Horizontal scan frequency: $625\times25=15625\ lines$
+ Time per line: $1/15,625 = 64 \mu sec(11.8+52.2)$
+ Vertical Retrace, reserved 25 lines per fields, 有效行数$625-25\times2=575\ lines$
### Color Model

+ YUV
+ Y-5.5Mhz 
+ U/V-1.8Mhz each

## 2.3 SECAM

+ 和PAL非常接近
+ 色彩不太一样

## 2.4 Comparison

![[ComparisonOfAnalogVideo.png]]
# 3. Digital Video

## 3.1 Advantages of digital representation

+ Storing video on digital devices or in memory
+ Ready to be processed and integrated into various multimedia applications
+ Direct access – nonlinear video editing
+ Repeated recording without degradation of image quality
+ Ease of encryption and better tolerance to channel noise

## 3.2 Chroma Subsamplng(考试一定会考)

+ 由于人类视觉系统对于色彩的敏感度较低，故可以通过色彩下采样，抛弃一些色彩数据从而实现图像信息的减少，但是不怎么影响图像质量。
+ 有以下常用的下采样方式：![[Pasted image 20240416155753.png]]

## 3.3 Digital video CCIR standard

+ 