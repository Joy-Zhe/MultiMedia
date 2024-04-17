# 1. Color Science
## 1.1 Light and Spectra

 + Light is an electromagnetic wave, its color characterized by the wavelength
	 Laser Light -- a single wavelength
	 Most light sources -- Contributions over many wavelengths
	 Short wave - Blue, Long wave -- Red
	 Visible light in the range ： 400-700nm 

## 1.2 Gamma Correction(非常重要)

+ 在电气元件上显示，需要将颜色转换为电压，基本思路是将0-255量化为0-1，但是实际上电压和实际的亮度值不成线性，会出现走样，需要构建一条新的曲线与之抵消，就像下图中的操作，通过Gamma校正将曲线变为与电气元件“互补”的曲线：![[0305_3.png]]

>  CRT显示器的非线性失真
+ CRT的亮度(light)和驱动电压(driving voltage)并不是线性关系，需要通过一个指数进行校正，这个指数就被称为Gamma($\gamma$)，即通过一个幂函数$V_{out}=V_{in}^{\gamma}$

+ 人的视觉系统存在问题，明暗度变化和实际明暗度并不是线性的，且对相对变化的明暗度比对绝对变化的明暗度更加敏感：![[0305_1.png]]
+ 人的视觉系统是非线性的，需要gamma校正
## 1.3 $L^*a^*b^*$


## 1.4 CMYK





