# PyDrop

Windows 64비트용 Python 파일 실행기입니다. `PyDrop.exe`에 `.py` 파일을 끌어다 놓거나, 실행기에서 파일을 선택한 후 실행할 수 있습니다. 출력과 오류는 실행기 창에서 확인할 수 있습니다.

Python을 따로 설치할 필요가 없습니다. 첫 실행 시 EXE에 포함된 Python 3.12.10 런타임을 `%LOCALAPPDATA%\PyDrop`에 풀어 사용합니다. 스크립트 폴더에 있는 다른 `.py` 모듈과 표준 라이브러리는 사용할 수 있습니다.

`pygame`, `requests` 같은 외부 패키지는 기본 포함되지 않습니다. 필요한 패키지를 해당 스크립트 옆의 `packages` 폴더에 Windows x64 / Python 3.12 호환 형태로 넣어야 합니다. `tkinter`와 `pip`는 Python 임베디드 배포판에 포함되지 않습니다.

## 빌드

```powershell
dotnet publish .\PythonRunner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

배포할 파일은 `publish\PyDrop.exe`입니다. 공식 Python 임베디드 배포판의 출처는 https://www.python.org/downloads/release/python-31210/ 입니다.
