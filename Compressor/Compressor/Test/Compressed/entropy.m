function entropy_value = entropy(image_path)
    img = imread(image_path);
    img_vector = img(:);  % 将图像展开为一个长向量
    [counts, ~] = imhist(img_vector);
    prob = counts / sum(counts);
    prob(prob == 0) = [];  % 移除概率为0的项
    entropy_value = -sum(prob .* log2(prob));
end
