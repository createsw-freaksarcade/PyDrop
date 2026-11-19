# PyDrop

Windows 64비트용 Python 프로젝트 실행기입니다. `PyDrop.exe`에 `.py` 파일이나 프로젝트 폴더를 끌어다 놓을 수 있습니다. 폴더를 선택하면 `main.py`, `app.py`, `__main__.py` 순서로 시작 파일을 찾습니다. 실행기에서 파일이나 폴더를 직접 선택할 수도 있습니다. 출력과 오류는 실행기 창에서 확인할 수 있습니다.

Python을 따로 설치할 필요가 없습니다. 첫 실행 시 EXE에 포함된 Python 3.12.10 런타임을 `%LOCALAPPDATA%\PyDrop`에 풀어 사용합니다. 프로젝트 폴더를 작업 디렉터리로 사용하므로 상대 경로의 이미지, 음원, 데이터와 다른 `.py` 모듈을 그대로 읽을 수 있습니다.

프로젝트 폴더에 `requirements.txt`가 있으면 첫 실행 때 필요한 패키지를 자동 설치해 `%LOCALAPPDATA%\PyDrop\packages`에 보관합니다. 리듬게임에 필요한 `pygame`, OpenCV, ModernGL, mutagen 및 의존 패키지는 EXE에 포함되어 인터넷 없이 설치됩니다. 다른 패키지가 필요하면 온라인 설치를 시도합니다. 이후 같은 요구사항은 캐시를 재사용합니다. 수동으로 준비한 패키지는 프로젝트의 `packages` 폴더에서도 읽습니다. `requirements.txt`가 없는 프로젝트는 필요한 외부 패키지를 자동으로 알 수 없습니다. `tkinter`는 Python 임베디드 배포판에 포함되지 않습니다.

## 빌드

```powershell
dotnet publish .\PythonRunner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

배포할 파일은 `publish\PyDrop.exe`입니다. 공식 Python 임베디드 배포판의 출처는 https://www.python.org/downloads/release/python-31210/ 입니다.
