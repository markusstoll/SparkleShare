# [SparkleShare](https://www.sparkleshare.org/)

SparkleShare was **created by [Hylke Bons](https://github.com/hbons)**. Most of the design, code, and years of maintenance are his work. This repository is a **community continuation** maintained by [Markus Stoll](https://github.com/markusstoll) after the [original project](https://github.com/hbons/SparkleShare) was discontinued — same name, same spirit, with a modernized macOS stack (4.x) and ongoing fixes.

**macOS builds (4.x):** [Releases on markusstoll/SparkleShare](https://github.com/markusstoll/SparkleShare/releases)  
**Original project & history:** [hbons/SparkleShare](https://github.com/hbons/SparkleShare) · [sparkleshare.org](https://www.sparkleshare.org/)

> [!NOTE]
> The upstream repository is archived. For current macOS previews and migration work, use this fork. For the story behind the original project, see 🌱 [issue #2006](https://github.com/hbons/SparkleShare/issues/2006).

<br>

[SparkleShare](https://www.sparkleshare.org/) is a file sharing and collaboration app. It works just like Dropbox, and you can run it on your own server. It's available for Linux distributions, macOS, and Windows.

![Banner](https://raw.githubusercontent.com/hbons/SparkleShare/master/SparkleShare/Common/Images/readme-banner.png)

You can support **Hylke Bons**, the creator of SparkleShare, through [💕 GitHub Sponsors](https://github.com/sponsors/hbons).

## How does it work?

SparkleShare creates a special folder on your computer. You can add remotely hosted folders (or "projects") to this folder. These projects will be automatically kept in sync with both the host and all of your peers when someone adds, removes or edits a file.

## Install on Ubuntu or Fedora

You can install the package from your distribution (likely old and not updated often), but we recommend to get our Flatpak with automatic updates to always enjoy the latest and greatest:

```bash
flatpak remote-add flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak install flathub org.sparkleshare.SparkleShare
```

Now you can run SparkleShare from the apps menu.

**Note:** by default SparkleShare uses an AppIndicator status icon on Linux. If you use GNOME on a distribution other than Ubuntu, please install the [AppIndicator extension](https://extensions.gnome.org/extension/615/appindicator-support/). If you don't use GNOME, you can start SparkleShare with `--status-icon=gtk`.


## Install on macOS

Download signed, notarized builds from the [releases page](https://github.com/markusstoll/SparkleShare/releases) (arm64 and Intel DMGs for 4.x previews). Building from source: see [SparkleShare/Mac/README.md](SparkleShare/Mac/README.md).


## Set up a host

Under the hood SparkleShare uses the version control system [Git](https://git-scm.com/) and the large files extension [Git LFS](https://git-lfs.github.com), so setting up a host yourself is relatively easy. Using your own host gives you more privacy and control, as well as lots of cheap storage space and higher transfer speeds. We've made a simple [script](https://github.com/hbons/Dazzle) that does the hard work for you. If you need to manage a lot of projects and/or users we recommend hosting a [GitLab Community Edition](https://about.gitlab.com/installation/) instance.


## Build from source
`SparkleShare` is Free and Open Source software and licensed under the [GNU GPLv3 or later](LICENSE.md). You are welcome to change and redistribute it under certain conditions. Its library `Sparkles` is licensed under the [GNU LGPLv3 or later](LICENSE_Sparkles.md).

Here are instructions to build SparkleShare on [Linux distributions](SparkleShare/Linux/README.md), [macOS](SparkleShare/Mac/README.md), and [Windows](SparkleShare/Windows/README.md).


## Useful links
- [sparkleshare.org](https://www.sparkleshare.org/) — original product site (Hylke Bons)
- [hbons/SparkleShare](https://github.com/hbons/SparkleShare) — original source & AUTHORS
- [markusstoll/SparkleShare](https://github.com/markusstoll/SparkleShare) — maintained fork, issues & releases
- [@hbons on Mastodon](https://mastodon.social/@hbons)
- [Wiki](https://www.github.com/hbons/SparkleShare/wiki)


Have fun, make awesome. :)
