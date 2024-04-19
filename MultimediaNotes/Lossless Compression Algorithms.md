## 1. Data Compression Scheme

## 1.1 Compression Ratio

+ 压缩前大小/压缩后大小

## 1.2 Basics of Information Theory

+ 信源的熵 entropy of an information sourse，代表最短的平均码长
+ 码表 alphabet $S={s_1, s_2, ..., s_n}$
+ entropy $$\eta = H(S)=\Sigma_{i=1}^np_ilog_2{1\over p_i}=-\Sigma_{i=1}^np_ilog_2{p_i}$$
+ 上式中$p_i$代表符号$s_i$在信源里出现的频率
+ $log_2{1\over p_i}$是单个符号在该信源中的熵，所以信源的熵实际上是所有符号的熵的加权平均，$log_2{1\over p_i}$也代表了这个符号需要的最短码长
> 例：第二张图的熵更小，因为第一张图的可能性更多，实际上计算得出的结果也是如此![[0315_1.png]]

# 2. Lossless Coding Algorithms

## 2.1 Run-Length Coding 游程编码

+ 记录一个符号的值和连续出现的次数，例：
![[0315_2.png]]
+ 不一定符合熵的规律
## 2.2 Variable-Length Coding

+ 可变长编码，符合信息熵的规律(最短平均码长大于等于信源的熵)
+ 频率越高，分配的码长越短
### Huffman Coding

+ Huffman编码的前缀是唯一的，解码时只用匹配码表中有的串即可，因为Huffman树的结构决定了这一性质

### Adaptive Huffman Coding 

+ 自适应Huffman编码
+ 一般的Huffman编码需要知道整个信源的信息，但是实际上可能无法得知完整信源信息，比如一段正在传输的数据，我们只能知道其目前的信息
+ 通过得到的信息不断地进行动态调整Huffman Tree
+ 保证encoder和decoder端的树保持一致，维护动态的平衡树即可

##  2.3 Dictionary-Based Coding 字典编码

### LZW
> 在GIF中应用
+ 一旦一个字符串是未出现过的，称之为一个单词，将其加入字典并分配一个编码
+ 假设输入序列为ABABBABCABABBA，则有以下编码过程：![[0315_3.png]]

## 2.4 Arithmetic Coding

+ 不受熵编码的规则限制
+ 以下是一个例子，下表是一个信源的信息，使用该信息编码CAEE$：![[0315_4.png]]

+ $CAEE\$$中，C占用0.3~0.5，A的出现概率为0.2，则A获得的区间大小为$(0.5-0.3)\times 0.2=0.04$，故A占用0.3~0.34，以此类推，有以下可视化过程：![[0315_5.png]]
+ 最终在最后一个区间内随便取个数就是该字符串的编码结果
+ 解码的过程如下(假设选取了Value=0.33203125)，从C开始解码，再获得A的解码($(Value_C-Low_C)\times 0.2$)，以此类推：![[0315_7.png]]

# 3. Lossless Image Compression

## 3.1 Differential Coding of image

+ 尽可能降低残差值