Kino/Motion
===========

*Motion* is a post-processing effect that simulates motion blur caused by
object/camera movement. It's based on the paper "[A Fast and Stable
Feature-Aware Motion Blur Filter][Guertin2014]" written by Jean-Philippe
Guertin et al.

![Gif][Gif1]
![Gif][Gif2]

*Motion* is part of the *Kino* effect suite. See the [GitHub repositories]
[Kino] for other effects included in the suite.

System Requirements
-------------------

- Unity 5.4 or later versions

*Motion* requires [motion vectors][MotionVectors] that is newly introduced in
Unity 5.4. Motion vector rendering is only supported on the platforms that has
RGHalf texture format support. This requirement must be met in most of the
desktop/console platforms, but rarely supported in the mobile platforms.

License
-------

Copyright (C) 2016 Keijiro Takahashi

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

[Guertin2014]: http://graphics.cs.williams.edu/papers/MotionBlurHPG14/
[Gif1]: https://66.media.tumblr.com/7bd939ad9c9c66a4d5191ba7a5d1391a/tumblr_o7h7la8h6Q1qio469o1_400.gif
[Gif2]: https://67.media.tumblr.com/9d906d5032d9d7fb5360b08a1a57aea9/tumblr_o7kgh97DKO1qio469o1_400.gif
[Kino]: https://github.com/search?q=kino+user%3Akeijiro&type=Repositories
[MotionVectors]: http://docs.unity3d.com/540/Documentation/ScriptReference/DepthTextureMode.MotionVectors.html
