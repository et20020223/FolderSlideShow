# WebP native libraries

The application bundles libsharpyuv.dll, libwebp.dll, and libwebpdemux.dll from Imazen.WebP.NativeRuntime.win-x64 1.6.1. The decoder reports its upstream version separately through WebPGetDecoderVersion.

- Package: https://www.nuget.org/packages/Imazen.WebP.NativeRuntime.win-x64/1.6.1
- Package SHA-256: 32DF07F31F18B5F4E35409A73621D776D97761F4B601CBBBDC4EFBACB6AB62F6
- Packaging project: https://github.com/imazen/libwebp-net
- Upstream source: https://github.com/webmproject/libwebp/tree/v1.6.0

See libwebp-COPYING.txt, libwebp-AUTHORS.txt, libwebp-PATENTS.txt, and imazen-LICENSE.txt in this directory for notices and terms. The application's WebpImage.cs calls the upstream C API directly and does not depend on a managed wrapper at runtime.
