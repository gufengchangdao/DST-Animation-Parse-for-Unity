该插件可以解析饥荒动画文件，并以unity的方式生成动画和预制件。

## 1. 安装

在包管理器中通过git URL来添加
```
https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity.git?path=/src/DSTAnimParse
```

DSTAnimDemo.unitypackage是示例场景。


## 2. 饥荒动画介绍
1. 饥荒动画是由bank和build组合而成的，bank负责记录有多少动画，每个动画每帧每个元素的变换（平移、缩放、旋转）以及显示哪个插槽（symbol）的哪个图，build负责记录贴图信息，找到图集进行切片和命名，记录哪些图是属于哪个插槽，因此一个bank可以对应不同的build（换皮），一个build可以对应不同的bank（不同的动画）。
2. 饥荒预制件的脚本文件路径可以参考下面的截图，steam右键饥荒联机版浏览本地文件就能找到，第一次找的话scripts是一个压缩包，需要解压一下。下图的spear是长矛的预制件文件，在预制件的文件里可以看到长矛用什么动画文件、什么bank、什么build

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/image.png)

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/spear_asset.png)

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/spear_code.png)

3. 饥荒动画播放中会修改动画元素（下图左边的部分）的layer，layer表示这个单位的某一部位，一个layer可能对应好几个元素，比如两个猪耳的layer都为ear，可以看到猪人的左手有两个，一个是ARM_carry_up，一个是arm_normal，分别表示手持东西时的手和正常下的手，饥荒里可以通过AnimState:Hide()和AnimState:Show()来控制哪些部位隐藏哪些部位显示，在unity里就是控制SpriteRender对象的显示和隐藏了。

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/pig_anim.png)

4. 饥荒动画是通过控制插槽（symbol）显示的图片来播放动画的，比如某个动画某一帧每个元素用到了哪个插槽的哪张图，饥荒里就可以通过owner.AnimState:OverrideSymbol()来让某些元素显示别的图，比如对于玩家手持武器，下图的长矛就是玩家名为swap_object的插槽，平常动画维护这个插槽里元素的变换，不过因为build里没有相关名字的图片所以就不会显示，通过OverrideSymbol来使动画可以找到对应名字的图片显示出来，在unity里就是控制SpriteRender显示哪张图了。

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/wilson.png)

## 3. 使用说明
1. 把动画压缩包拖拽进去，然后填写bank、build、预制件名，点击生成即可生成对应的预制件。bank和build填什么可能得翻看一下饥荒lua脚本了。

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/tool_1.png)

2. bank文件可以选多个，会把bank里所有相关的动画都生成对应的clip文件。

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/tool_2.png)
![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/wilson_unity_1.png)

3. 该工具没做到bank和build分离，因此对于同一种bank动画和不同build贴图你可能得生成不同的预制件了。
4. 动画控制器、动画clip、预制件都是没有才生成，因此不会覆盖掉修改后的文件。
5. 每个预制件会生成该预制件所有动画可能会用到的子对象，比如下面的arm_upper_xx这样的对象有4个，有的动画可能一个都不需要，但是肯定有个动画symbol为arm_upper的动画会用到4个，swap_object会用于装备武器时显示武器贴图的。

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/wilson_unity_2.png)

6. 默认给生成的预制件添加了DSTAnimator，为动画的播放提供一些便捷：
  - 可以监听动画播放完的事件。
  - 动画播放过程中动态修改子对象的layer，控制哪些layer的显示和隐藏。
  - 替换动画插槽的贴图，就是实现类似owner.AnimState:OverrideSymbol("swap_object", "swap_spear", "swap_spear")这样的功能。
7. 给状态添加了BaseStateBehaviour，旨在动画和状态分离，实现同一种动画控制器不同逻辑的单位。
8. 解包后的clip动画有些可能需要手动设置一下循环播放，动画控制器里的状态可以连连线，添加添加动画属性，对于像饥荒里不同角度显示不同朝向的动画可以像我这样用混合状态来实现。

![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/pig_unity_1.png)
![图片](https://github.com/gufengchangdao/DST-Animation-Parse-for-Unity/blob/main/docs/screenshots/pig_unity_2.png)

9. 音效不在该工具范畴，如果想解包饥荒的音效，可以试试FSB Extractor
饥荒音频提取工具及使用方法



## 4. 解包实现思路
这个是我解包饥荒动画时做的笔记，记录有饥荒动画文件的数据格式。

[文件说明和解析思路](https://iitkra4fu8q.feishu.cn/docx/JQw4drN8io3ylFxdeODcDzvrnbd?from=from_copylink)
