# 1. Basic Data Types

## 1.1 1-bit image

+ binary image / monochrome image
+ 0/1表示的单个像素，像素只有黑/白两种选择
+ 0-black, 1-white

## 1.2 8-bit Grey-Level image

+ 0-black, 255-white
+ 单个像素8-bit，有明暗变化
+ DPI(dots per inch)

> 黑白打印机只有黑色油墨，为什么能打出灰度图？
+ convert the color resolution into the spatial resolution
+ 实际上打印机在打一个像素时，会打很多点，通过控制一个像素内打上黑色点的个数来实现不同的灰度
+ 比如一个8-bit灰度图，有256个灰度等级，故一个像素需要$16\times 16$的点来表示，所以打印机驱动需要将一张灰度图**先转化为大小是原先256倍的1-bit image**再传输给打印机进行打印
+ 使用如上方法，$N*N$大小的矩阵可以表示$N^2+1$个灰度等级：![[3.2_1.png]]
### 优化

> 如何防止图像的大小变化？
+ 通过一个预设好的图像，与当前像素值进行比较，如果矩阵中的值比当前像素值大，那么打印矩阵的对应位置就打黑色。例：![[3.2_2.png]]

## 1.3 24-bit color image

+ RGB 888
## 1.4 8-bit color image

+ 只能表示$2^8=256$种颜色
+ 图像存储的是**颜色的索引值**，通过这个像素的索引值，从图像的**颜色查找表**中找到对应的颜色进行显示。
+ 故显示的颜色是真彩色的，但颜色数量最大只有256种
+ 通过替换颜色查找表，可以方便的进行图片颜色的变换
+ **颜色查找表(Look Up Table)**: LUT，就是Photoshop中使用的那个

## How to devise a color lookup table?

+ 颜色直方图
+ 如何通过适当的切分来将颜色映射到256个颜色
1. 暴力切分，红色切成8份，绿色分成8份，蓝色分成4份（人眼对蓝色的感知较弱）
2. 中值切分：median-cut


# 2. Image file format

## 2.1 GIF

+ LZW compression algorithm
+ limited to 8-bit image

## 2.2 JPEG

+ lossy of lossless compression image

## 2.3 BMP

+ origin, no compression
+ compressed


# 3. Quiz

+ 