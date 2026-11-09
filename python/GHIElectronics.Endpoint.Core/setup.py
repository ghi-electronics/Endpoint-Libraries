# The settings are in pyproject.toml. This file marks the wheel as one for
# the board's processor: v0.5.x ships both 32-bit EPM815 (armv7l) and 64-bit
# EPN256 / EPN128 (aarch64) support in the same wheel by bundling two
# libendpoint*.so files. pip accepts the wheel on either arch because both
# platform tags are listed (dot-separated).
from setuptools import setup
from setuptools.command.bdist_wheel import bdist_wheel

PLATFORM = "manylinux_2_34_aarch64.manylinux_2_34_armv7l"


class BoardWheel(bdist_wheel):
    def finalize_options(self):
        super().finalize_options()
        self.root_is_pure = False

    def get_tag(self):
        return "py3", "none", PLATFORM


setup(cmdclass={"bdist_wheel": BoardWheel})
