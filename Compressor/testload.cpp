//
// Created by Joy Zheng on 2024/3/16.
//

#define STB_IMAGE_IMPLEMENTATION
#include "Dependencies/stb_image.h"
#include <iostream>
#include <string>
#include <vector>

void test() {
    std::string inputPath = "./test/testimg.bmp";
    int w, h, n;
    unsigned char *idata = stbi_load(inputPath.c_str(), &w, &h, &n, 0);
}
