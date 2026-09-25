# Brand assets

Drop the supplied Soft & Co. logo files here.

The brand guide is explicit on this point:

> Never redraw or alter the logo, doing so weakens our identity.

So nothing in this project recreates the SC monogram. Until the real files are
added, `_Layout.cshtml` shows the wordmark set in the display typeface as an
interim stand-in.

## Files to add

| File | Source | Used for |
|---|---|---|
| `softco-icon.svg` | EPS from the brand pack, exported to SVG | Sidebar mark, favicon |
| `softco-horizontal.svg` | Primary logo, horizontal stack | Wide headers, letterhead-style exports |
| `softco-vertical.svg` | Primary logo, vertical stack | Login screen, splash |
| `favicon.ico` | Icon / monogram | Browser tab |

Prefer SVG (from the EPS) over JPG — the guide notes the EPS is vector and stays
crisp at any size, while the JPG cannot be scaled without loss.

## Swapping the wordmark for the real asset

In `Views/Shared/_Layout.cshtml`, replace:

```html
<span class="sc-brand-word">Soft&amp;Co.</span>
```

with:

```html
<img src="~/img/brand/softco-horizontal.svg" alt="Soft & Co." class="sc-brand-img" />
```

and add a width rule for `.sc-brand-img` in `wwwroot/css/site.css`. The logo must
sit on Warm Beige, Burgundy, Shadow or Titanium White only — use the version of
the asset drawn for that background rather than recolouring it.
