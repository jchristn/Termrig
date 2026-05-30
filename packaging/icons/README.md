# Packaging Icons

The canonical source icon is:

```text
src/Termrig.App/Assets/termrig-logo.svg
```

Checked-in packaging icon assets:

```text
packaging/icons/termrig.ico
packaging/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg
```

Generate raster Linux icons with ImageMagick:

```sh
for size in 16 32 48 64 128 256 512; do
  mkdir -p "packaging/icons/hicolor/${size}x${size}/apps"
  magick -background none src/Termrig.App/Assets/termrig-logo.svg \
    -resize "${size}x${size}" \
    "packaging/icons/hicolor/${size}x${size}/apps/com.jchristn.Termrig.png"
done
```

Generate a macOS `.icns` file on macOS:

```sh
mkdir -p packaging/icons/Termrig.iconset
for size in 16 32 128 256 512; do
  magick -background none src/Termrig.App/Assets/termrig-logo.svg \
    -resize "${size}x${size}" \
    "packaging/icons/Termrig.iconset/icon_${size}x${size}.png"
  magick -background none src/Termrig.App/Assets/termrig-logo.svg \
    -resize "$((size * 2))x$((size * 2))" \
    "packaging/icons/Termrig.iconset/icon_${size}x${size}@2x.png"
done
iconutil -c icns packaging/icons/Termrig.iconset -o packaging/icons/Termrig.icns
```

Validate generated icons on light and dark backgrounds before publishing a
release.
