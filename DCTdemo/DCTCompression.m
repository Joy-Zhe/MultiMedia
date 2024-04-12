% 读取图像
img = imread('R-C.bmp'); % 替换 'your_image.jpg' 为你的图像文件路径
if size(img, 3) == 3
    % 转换颜色空间 RGB 到 YCbCr
    imgYCbCr = rgb2ycbcr(img);
    img = imgYCbCr(:, :, 1); % 只处理Y分量，简化示例
end
img = double(img); % 转换为double类型，进行DCT

% 处理边界，使图像的尺寸能被8整除
rows = size(img, 1);
cols = size(img, 2);
padRows = 8 - mod(rows, 8);
if padRows == 8
    padRows = 0;
end
padCols = 8 - mod(cols, 8);
if padCols == 8
    padCols = 0;
end
img = padarray(img, [padRows padCols], 'replicate', 'post');
% 标准JPEG量化矩阵（亮度）
Q = [16 11 10 16 24 40 51 61; 
     12 12 14 19 26 58 60 55;
     14 13 16 24 40 57 69 56;
     14 17 22 29 51 87 80 62;
     18 22 37 56 68 109 103 77;
     24 35 55 64 81 104 113 92;
     49 64 78 87 103 121 120 101;
     72 92 95 98 112 100 103 99];
% 分块和DCT变换
[rows, cols] = size(img);
dctBlocks = zeros(rows, cols);
for i = 1:8:rows
    for j = 1:8:cols
        block = img(i:i+7, j:j+7);
        dctBlock = dct2(block);
        dctBlocks(i:i+7, j:j+7) = dctBlock;
    end
end
disp(dctBlocks(1,1))
% 绘图
figure;
imagesc(log(abs(dctBlocks) + 1)); % 使用log变换来增强可视化效果
colormap('jet'); % 使用jet颜色图
colorbar;
title('DCT变换后的图像');
% 量化
quantizedDCTBlocks = zeros(rows, cols);
for i = 1:8:rows
    for j = 1:8:cols
        dctBlock = dctBlocks(i:i+7, j:j+7);
        quantizedBlock = round(dctBlock ./ Q);
        quantizedDCTBlocks(i:i+7, j:j+7) = quantizedBlock;
    end
end
disp(quantizedDCTBlocks(1,1))
% 绘图
figure;
imagesc(log(abs(quantizedDCTBlocks) + 1)); % 使用log变换来增强可视化效果
colormap('jet'); % 使用jet颜色图
colorbar;
title('量化后的图像');
% Zigzag扫描的一个示例块
zigzagIndex = [ 1 2 9 17 10 3 4 11 18 25 33 26 19 12 5 6 13 20 27 34 41 49 42 35 28 21 14 7 8 15 22 29 36 43 50 57 58 51 44 37 30 23 16 24 31 38 45 52 59 60 53 46 39 32 40 47 54 61 62 55 48 56 63 64];
zigzagOrder = reshape(zigzagIndex, [8,8]);
quantizedZigzag = zeros(size(quantizedDCTBlocks));
for i = 1:8:rows
    for j = 1:8:cols
        block = quantizedDCTBlocks(i:i+7, j:j+7);
        zigzagBlock = block(zigzagOrder);
        quantizedZigzag(i:i+7, j:j+7) = zigzagBlock;
    end
end
