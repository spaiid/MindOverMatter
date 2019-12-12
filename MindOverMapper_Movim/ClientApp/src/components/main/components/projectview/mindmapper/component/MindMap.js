import React from "react";
import axios from 'axios';
import Keys from "fbjs/lib/Keys";
import {
  DiagramWidget,
  MindMapModel,
  DiagramState,
  OpType,
  FocusItemMode
} from "blink-mind-react";
import CloseIcon from '@material-ui/icons/Close';
import { Toolbar } from "./Toolbar";
import CheckCircleIcon from '@material-ui/icons/CheckCircle';
import IconButton from '@material-ui/core/IconButton';

import './MindMap.css';
import LoadingGIF from '../../../../../../static/Loading3.gif';
import Snackbar from '@material-ui/core/Snackbar';
import SnackbarContent from '@material-ui/core/SnackbarContent';
import { makeStyles } from '@material-ui/core/styles';
import { amber, green } from '@material-ui/core/colors';


const useStyles1 = makeStyles(theme => ({
  success: {
    backgroundColor: green[600],
  },
  error: {
    backgroundColor: theme.palette.error.dark,
  },
  info: {
    backgroundColor: theme.palette.primary.main,
  },
  warning: {
    backgroundColor: amber[700],
  },
  icon: {
    fontSize: 20,
  },
  iconVariant: {
    opacity: 0.9,
    marginRight: theme.spacing(1),
  },
  message: {
    display: 'flex',
    alignItems: 'center',
  },
}));

export default class MindMap extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      userData: this.props.userData,
      projectInfo: this.props.projectInfo,
      change: false,
      diagramVersion: 0,
      openSuccessSnackBar: false,
      oldState: null,
      newState: null
    };
  }

  componentDidMount = async () => {
    await this.retrieveMindMap(this.props.projectInfo.uid);
    this.autoSave();
  }

  getFocusItemMode() {
    return this.state.diagramState.mindMapModel.getFocusItemMode();
  }

  autoSave = async () => {
    await setTimeout(async () => {
      if(this.state.diagramState === this.state.oldState){
        this.autoSave();
      } else {
        await this.saveMindMap();
        this.setState({
          oldState: this.state.diagramState
        });
        this.autoSave();
      }
    }, 10000);
  }

  onChange = diagramState => {
    console.log("onChange");
    this.setState({
      diagramState,
      change: true
     });
  };

  op = (opType, nodeKey, arg) => {
    let { diagramState } = this.state;
    let newState = DiagramState.op(diagramState, opType, nodeKey, arg);
    this.onChange(newState);
  };

  retrieveMindMap = async (projectId) => {
    let mindModelConfig = await axios.get(`/api/project/${projectId}/state`, {
      headers: {
        Authorization: 'Bearer ' + this.state.userData.token //the token is a variable which holds the token
      }
    }).then(res => res.data);

    //res.data.version for getting the version

    console.log(mindModelConfig)
    
    let mindModel = MindMapModel.createWith(mindModelConfig.state);

    let diagramConfig = {
      hMargin: 100,
    };
    let diagramState = DiagramState.createWith(mindModel, diagramConfig);

    this.setState({
      oldState: diagramState,
      diagramState: diagramState,
      diagramVersion: mindModelConfig.version
    });
  }


  saveMindMap = async () => {
    let items = [];

    this.state.diagramState._immutable.model.itemMap.forEach(item => {
      items.push(item);
    });

    let diagramConfig = {
      editorRootItemKey: this.state.diagramState._immutable.model.editorRootItemKey,
      items: items,
      rootItemKey: this.state.diagramState._immutable.model.rootItemKey
    }

    let diagramJSON = JSON.stringify(diagramConfig);

    console.log(diagramJSON);
    console.log(this.state.userData.token)
    console.log(this.state.diagramVersion);
  

    axios({
      method: 'put',
      url: `/api/project/${this.props.projectInfo.uid}/state`,
      headers: {
                Authorization: 'Bearer ' + this.state.userData.token, //the token is a variable which holds the token
                'Content-Type': 'application/json'
              },
      data: JSON.stringify({
        "state" : diagramConfig,//{"editorRootItemKey":"root","items":[{"key":"init1","parentKey":"root","subItemKeys":[],"collapse":false,"content":"test","desc":"test ttt: \\(ttt\\) t"},{"key":"root","parentKey":null,"subItemKeys":["init1"],"collapse":false,"content":"test","desc":null}],"rootItemKey":"root"},
        "version" : this.state.diagramVersion,
      }) 
    }).then(response => { 
      // Saved successfully
      console.log(response)
      this.handleOpenSuccessSnackBar(response.data.version)
    })
    .catch(error => {
        // Save conflict
        console.log(error.response)
        this.handleOpenErrorSnackBar()
        this.retrieveMindMap(this.props.projectInfo.uid);
      });;
  }

  viewThemes = () => {
    console.log(this.state.diagramState.config)
  }

  changeTheme = () => {
    let diagramState = this.state.diagramState;

    diagramState._immutable.config.themeConfigs = {
      theme3: {
        color: {
          primary: "lightgreen", // supports hex or name
          fontColor: "black"
        },
        name: "Easter Green"
      }
    }

    diagramState._immutable.config.normalItemStyle = {
      borderRadius: "6px",
      fontSize: "16px",
      padding: "0",
      color: { 
        primary: 'brown',
        fontColor: 'black'
      }
    }

    diagramState._immutable.config.theme = "theme3";

    this.setState({
      diagramState
    });
  }

  handleOpenSuccessSnackBar = (version) => {
    this.setState({openSuccessSnackBar : true,
                    diagramVersion: version
    })
}
handleCloseSuccessSnackBar = () =>  {
    this.setState({openSuccessSnackBar : false})
}

handleOpenErrorSnackBar = () => {
  this.setState({openErrorSnackBar : true})
}
handleCloseErrorSnackBar = () =>  {
  this.setState({openErrorSnackBar : false})
}

  render() {
    return (
      <div className="mindmap">
        {this.state.diagramState ? (
          <div className="mindmap">
            <Toolbar
            diagramState={this.state.diagramState}
            onChange={this.onChange}
            op={this.op}
            changeTheme={this.changeTheme}
            saveMindMap={this.saveMindMap}
             />
            <DiagramWidget
              diagramState={this.state.diagramState}
              onChange={this.onChange}
            />
          </div>
        ) : (
            <div >
              <img src={LoadingGIF} className='loading-content' />
            </div>
          )}

        <Snackbar
          anchorOrigin={{
            vertical: 'top',
            horizontal: 'center',
          }}
          open={this.state.openSuccessSnackBar}
          autoHideDuration={3000}
          onClose={this.handleCloseSuccessSnackBar}
          ContentProps={{
            'aria-describedby': 'message-id',
          }}
          variant="success"
          message={<span id="message-id"><CheckCircleIcon /> Saved Successfully!</span>}
          action={[
            <IconButton
              key="close"
              aria-label="Close"
              color="red"

              onClick={this.handleCloseSuccessSnackBar}
            >
              <CloseIcon />
            </IconButton>,
          ]}
        />
        <Snackbar
          id='success-snack'
          anchorOrigin={{
            vertical: 'top',
            horizontal: 'center',
          }}
          open={this.state.openErrorSnackBar}
          autoHideDuration={6000}
          onClose={this.handleCloseErrorSnackBar}
          ContentProps={{
            'aria-describedby': 'message-id',
          }}
          message={<span id="message-id"><CheckCircleIcon /> Error Saving, Changes Reverted!</span>}
          action={[
            <IconButton
              key="close"
              aria-label="Close"
              color="red"

              onClick={this.handleCloseErrorSnackBar}
            >
              <CloseIcon />
            </IconButton>,
          ]}
        />
      </div>
    );
  }
}
