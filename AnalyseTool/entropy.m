function entropy_value = entropy(image_path)
    img = imread(image_path);
    if size(img, 3) == 3  % Check if the image is RGB
        img_gray = rgb2gray(img);
    else
        img_gray = img;
    end
    [counts, ~] = imhist(img_gray);
    prob = counts / sum(counts);
    prob(prob == 0) = [];  % Remove zero entries
    entropy_value = -sum(prob .* log2(prob));
end