# ZJU MultiMedia Course

## 1. 图像压缩

+ DCT

### 1.1 Data Structure

+ BGRA
+ YUV
+ ImgData
  - RGB与YUV的转换

### 1.2 Data Loader

+ 读取本地图像, 将图像存储至ImgData

> 以BGRA8格式为例，每个像素占用4个字节，分别是B、G、R、A

### 1.3 DCT

+ 对分块后的图像进行DCT变换

### 1.4 Quantize

+ 对DCT变换后的图像进行量化

### 1.5 Zigzag

+ 将量化后的图像进行Zigzag扫描
+ `zigzag[0]`表示DC，`zigzag[1]~zigzag[63]`表示AC
+ DC

### 1.6 Huffman

+ 对Zigzag扫描后的图像进行Huffman编码

### 1.7 Save binary file

+ 文件结构
```
  --------------------------
  |         Header         |
  --------------------------
  |          Data          |
  --------------------------
```

+ 文件结构(header)
> 由于不记录块的编号，所以要通过图像原始大小来进行块的重新编号，将一维转化为二维
```
  -----------------------------------
  |         Image Width (4B)        |
  -----------------------------------
  |         Image Height (4B)       |
  -----------------------------------
  |           quality (4B)          |
  -----------------------------------
  |     Down sampling type (1B)     |  // 4:4:4: 0x00, 4:2:0: 0x01, 后续可再做拓展
  -----------------------------------
  |       Dictionary Size (4B)      |  // Luminance DC Dictionary Count
  -----------------------------------
  |      Luminance DC Dictionary    |  // (int, string) pairs
  -----------------------------------
  |       Dictionary Size (4B)      |  // Luminance AC Dictionary Count
  -----------------------------------
  |      Luminance AC Dictionary    |  // (int, string) pairs
  -----------------------------------
  |       Dictionary Size (4B)      |  // Chrominance DC Dictionary Count
  -----------------------------------
  |    Chrominance DC Dictionary    |  // (int, string) pairs
  -----------------------------------
  |       Dictionary Size (4B)      |  // Chrominance AC Dictionary Count
  -----------------------------------
  |    Chrominance AC Dictionary    |  // (int, string) pairs
  -----------------------------------
```
+ 文件结构(Data)
``` 
  -----------------------------------
  |          Y channel DC           |
  -----------------------------------
  |          U channel DC           |
  -----------------------------------
  |          V channel DC           |
  -----------------------------------
  |          Y channel AC           |
  -----------------------------------
  |          U channel AC           |
  -----------------------------------
  |          V channel AC           |
  -----------------------------------
```

+ Channel Data
```
  --------------------------
  |    channel Type (2B)   |  // 'Y', 'U', 'V', the .NET char type using UTF-16
  --------------------------
  |       DC/AC (1B)       |  // ifAc, 0:DC, 1:AC
  --------------------------
  |additional bits len (1B)|
  --------------------------
  |    bits length(2B)     |  // (存储补齐后的比特流所占的字节数)
  --------------------------
  |    Compressed bits     |
  --------------------------
```

## 2. 文本压缩

+ Huffman Compression 与 C#自带的压缩库比较，压缩率与速度
+ ...

### 2.1 Huffman Compression
1. 首先需要定义压缩后文件的结构
+ 文件后缀: ***.huf***
> 文件结构：文件头+压缩后的比特流
+ 文件结构：
```
  --------------------------
  |         Header         |
  --------------------------
  |       Dictionary       |
  --------------------------
  |    Compressed bits     |
  --------------------------
```
+ 文件头：需要记录最后补齐多加的bit0个数
```
  --------------------------
  |  Dictionary Size(8B)   |   
  --------------------------
  |  additional bits(1B)   |
  --------------------------
  |    bits length(8B)     |
  --------------------------
```
2. 巨大缺陷：
+ 有一种明显的情况，即当文本长度十分短，但是内容十分“丰富”——即有很多不同的字符时，压缩效果会非常差。
> 此时编码表的长度可能比压缩文本本身还长
